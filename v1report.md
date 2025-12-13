# Reporte de Auditoría Legacy - SocialPanelCore

**Fecha de revisión:** 2025-12-13
**Auditor:** Tech Lead / Staff Engineer (automatizado)
**Commit base:** d3767ab

---

## 1. RESUMEN EJECUTIVO

SocialPanelCore es una plataforma "mini-Hootsuite" para gestión y automatización de publicaciones en redes sociales, desarrollada en **Blazor Server (.NET 10.0)** con arquitectura Clean Architecture. El sistema permite:

- Gestionar cuentas/negocios y sus redes sociales (Facebook, Instagram, TikTok, X, YouTube, LinkedIn)
- Planificar publicaciones en calendario
- Adaptar contenido automáticamente con IA (OpenRouter) para cada red
- Publicar automáticamente vía Hangfire jobs

**Estado general: 85% funcional** - El core de negocio está implementado. La integración con APIs externas usa Refit pero depende de credenciales reales (actualmente FAKE). No hay tests unitarios/integración. La seguridad de tokens está cubierta (DataProtection) pero hay secretos hardcodeados en appsettings.json.

**Riesgos críticos:**
1. Credenciales expuestas en appsettings.json (contraseña BD, API keys)
2. Sin tests automatizados
3. Tokens de OAuth nunca se refrescan automáticamente
4. No hay rate limiting ni circuit breakers en APIs externas

---

## 2. ARQUITECTURA REAL

```
┌─────────────────────────────────────────────────────────────────────────┐
│                           PRESENTACIÓN                                  │
│  ┌─────────────────────────────────────────────────────────────────┐   │
│  │  Blazor Server (MudBlazor 8.15)                                 │   │
│  │  - Components/Pages/Publications (New, Edit, View, Preview)    │   │
│  │  - Components/Pages/Accounts, Users, SocialChannels, Reviews   │   │
│  │  - Components/Account (Identity: Login, Register)              │   │
│  └─────────────────────────────────────────────────────────────────┘   │
└───────────────────────────────────────┬─────────────────────────────────┘
                                        │ Injects Services
┌───────────────────────────────────────▼─────────────────────────────────┐
│                           DOMAIN INTERFACES                             │
│  SocialPanelCore.Domain/Interfaces/                                     │
│  - IAccountService, IUserService, IBasePostService                      │
│  - ISocialChannelConfigService, IContentAdaptationService               │
│  - ISocialPublisherService, IOAuthService, IMediaStorageService         │
│  - IAiContentService, IImmediatePublishService                          │
└───────────────────────────────────────┬─────────────────────────────────┘
                                        │ Implements
┌───────────────────────────────────────▼─────────────────────────────────┐
│                           INFRASTRUCTURE                                │
│  SocialPanelCore.Infrastructure/Services/                               │
│  - AccountService, UserService, BasePostService                         │
│  - SocialChannelConfigService (cifrado DataProtection)                  │
│  - ContentAdaptationService (orquesta IA)                               │
│  - AiContentService (OpenRouter API)                                    │
│  - SocialPublisherService (Refit: X, Meta, TikTok + Google SDK: YouTube)│
│  - OAuthService (Facebook, Instagram, LinkedIn, YouTube, TikTok)        │
│  - MediaStorageService (almacenamiento local)                           │
│  - ImmediatePublishService (flujo síncrono)                             │
│                                                                         │
│  SocialPanelCore.Infrastructure/ExternalApis/                           │
│  - X/IXApiClient (Refit - OAuth 1.0a)                                   │
│  - Meta/IMetaGraphApiClient (Refit - Graph API)                         │
│  - TikTok/ITikTokApiClient (Refit)                                      │
│  - YouTube/YouTubeApiService (Google SDK)                               │
└───────────────────────────────────────┬─────────────────────────────────┘
                                        │ Uses
┌───────────────────────────────────────▼─────────────────────────────────┐
│                           DATA LAYER                                    │
│  ApplicationDbContext (EF Core 10 + PostgreSQL Npgsql)                  │
│  Entities: Account, User, BasePost, AdaptedPost, PostMedia,             │
│            SocialChannelConfig, PostTargetNetwork, UserAccountAccess    │
│                                                                         │
│  BACKGROUND JOBS (Hangfire + PostgreSQL)                                │
│  - adaptar-contenido-ia: cada 3 horas                                   │
│  - publicar-posts-programados: cada 5 minutos                           │
└─────────────────────────────────────────────────────────────────────────┘
```

**Puntos clave:**
- Arquitectura Clean con separación Domain/Infrastructure
- Blazor Server con Interactive Server rendering
- PostgreSQL como BD única (también para Hangfire)
- MudBlazor para UI components
- Serilog para logging (Console + File rotation)
- DataProtection API para cifrado de tokens OAuth

---

## 3. TABLA DE FUNCIONALIDADES

| Feature (Funcional) | Evidencia en código | Estado | % | Gaps vs Funcional | Riesgos/Notas |
|---------------------|---------------------|--------|---|-------------------|---------------|
| **Gestión de Usuarios (2 roles)** | `IUserService`, `UserService.cs`, `Users/Index.razor` | HECHO | 85% | Falta envío email credenciales, falta impedir eliminar último superadmin | Roles hardcoded en Identity |
| **Gestión de Cuentas (negocios)** | `IAccountService`, `AccountService.cs`, `Accounts/Index.razor` | HECHO | 90% | Falta validación al eliminar cuentas con posts | Sin soft-delete |
| **Asignación Usuarios ↔ Cuentas** | `UserAccountAccess` entity, `UserService.cs` | PARCIAL | 60% | Modelo existe pero UI incompleta, no hay pantalla dedicada | Sin tests |
| **Conexión OAuth redes sociales** | `OAuthService.cs`, `OAuth/Callback.razor`, `ConfigureOAuthDialog.razor` | HECHO | 80% | Falta refresh automático de tokens expirados | Secretos FAKE en config |
| **Configuración X/Twitter (OAuth 1.0a)** | `OAuth1Helper.cs`, `ConfigureXCredentialsDialog.razor`, `SocialChannelConfigService` | HECHO | 90% | - | Requiere credenciales Developer |
| **Calendario de publicaciones** | `Publications/Index.razor`, `New.razor` | PARCIAL | 70% | Vista de lista, no hay componente calendario visual | UI básica |
| **Crear Publicación Base** | `IBasePostService.CreatePostAsync`, `New.razor` | HECHO | 95% | - | Maneja medios y IA |
| **Editar/Modificar Publicación** | `Edit.razor`, `UpdatePostAsync`, `UpdateNetworkConfigsAsync` | HECHO | 90% | No regenera adaptaciones automáticamente | Cambia a AdaptacionPendiente |
| **Subida de medios (imágenes)** | `IMediaStorageService`, `MediaStorageService.cs`, `StorageSettings` | HECHO | 95% | Solo imágenes por defecto (.jpg, .png) | Videos configurables |
| **Adaptación IA por red social** | `IAiContentService`, `AiContentService.cs`, `IContentAdaptationService` | HECHO | 85% | Fallback sin IA funciona; API key fake | OpenRouter integrado |
| **Preview antes de publicar** | `Preview.razor`, `IImmediatePublishService.GeneratePreviewsAsync` | HECHO | 95% | - | Permite editar antes de enviar |
| **Publicación programada (Hangfire)** | `PublishScheduledPostsAsync`, `RecurringJob`, Program.cs:221-224 | HECHO | 90% | - | Cada 5 min |
| **Publicación inmediata** | `ImmediatePublishService.PublishAfterPreviewAsync`, `PublishDirectlyAsync` | HECHO | 90% | - | Flujo síncrono |
| **Publicar en Facebook** | `IMetaGraphApiClient`, `PublishToFacebookWithRefitAsync` | HECHO | 85% | Requiere token real | Graph API v18 |
| **Publicar en Instagram** | `IMetaGraphApiClient`, `PublishToInstagramWithRefitAsync` | HECHO | 80% | REQUIERE imagen siempre | Container API implementada |
| **Publicar en X/Twitter** | `IXApiClient`, `PublishToXWithRefitAsync`, `OAuth1Helper` | HECHO | 90% | - | OAuth 1.0a |
| **Publicar en TikTok** | `ITikTokApiClient`, `PublishToTikTokWithRefitAsync` | HECHO | 75% | REQUIERE video/imagen | Polling status |
| **Publicar en YouTube** | `YouTubeApiService`, `PublishToYouTubeWithSdkAsync` | HECHO | 75% | REQUIERE video | Google SDK |
| **Publicar en LinkedIn** | `PublishToLinkedInAsync` (HttpClient) | HECHO | 80% | Sin soporte medios | API v2 |
| **Manejo errores y reconexión** | `UpdateHealthStatusAsync`, retry logic | PARCIAL | 60% | No hay reconexión automática de tokens expirados | Solo marca KO |
| **Gestión de Variantes/Adaptaciones** | `AdaptedPost` entity, `AdaptedPostState` enum | HECHO | 85% | - | Estados completos |
| **Estados de Publicación Base** | `BasePostState` enum (7 estados) | HECHO | 95% | Borrador, Planificada, AdaptacionPendiente, Adaptada, ParcialmentePublicada, Publicada, Cancelada | Completo |
| **Sistema de Revisiones** | `Reviews/Index.razor`, `ReviewDialog.razor`, `RequiresApproval` | PARCIAL | 50% | UI básica, ApprovePostAsync/RejectPostAsync implementados | Flujo incompleto |
| **Idioma es-ES único** | Prompts en español en `AiContentService.BuildPrompt` | HECHO | 100% | - | Hardcoded |

---

## 4. BUGS PROBABLES (PRIORIZADOS)

### BUG-001: Tokens OAuth nunca se refrescan automáticamente
**Severidad:** ALTA
**Síntoma:** Después de X horas/días, las publicaciones fallan con error de token expirado
**Causa:** `SocialPublisherService` no verifica `TokenExpiresAt` ni llama a `RefreshTokenAsync` antes de publicar
**Archivo:** `SocialPublisherService.cs:116-146`
**Repro:** Configurar cuenta OAuth, esperar a que expire token, intentar publicar
**Fix:**
1. Antes de publicar, verificar `channelConfig.TokenExpiresAt < DateTime.UtcNow.AddMinutes(5)`
2. Si está por expirar, llamar a `OAuthService.RefreshTokenAsync`
3. Actualizar tokens en BD

### BUG-002: Credenciales expuestas en appsettings.json
**Severidad:** ALTA
**Síntoma:** Contraseña de PostgreSQL y API keys visibles en repo
**Causa:** Secretos hardcodeados en `appsettings.json:3-4`, `:11`, `:18-45`
**Archivo:** `appsettings.json`
**Repro:** `cat appsettings.json | grep Password`
**Fix:**
1. Usar User Secrets en desarrollo: `dotnet user-secrets set "ConnectionStrings:DefaultConnection" "..."`
2. Usar variables de entorno en producción
3. Añadir `appsettings.json` a `.gitignore` o usar `appsettings.Production.json`

### BUG-003: URLs de medios son relativas, no absolutas
**Severidad:** MEDIA
**Síntoma:** Instagram/TikTok fallan al publicar porque necesitan URLs públicas absolutas
**Causa:** `PostMedia.Url` retorna `/uploads/...` que es relativo al servidor local
**Archivo:** `PostMedia.cs:67-69`, `SocialPublisherService.cs:153-156`
**Repro:** Crear post con imagen, intentar publicar en Instagram
**Fix:**
1. Configurar `BaseUrl` en appsettings
2. Modificar `PostMedia.Url` para usar URL absoluta pública
3. O usar almacenamiento cloud (S3, Azure Blob)

### BUG-004: StorageSettings no tiene todas las propiedades usadas
**Severidad:** MEDIA
**Síntoma:** `MediaStorageService` usa propiedades que podrían no existir (`MaxVideoFileSizeBytes`, `AllowVideoUpload`)
**Causa:** `StorageSettings` configurado en appsettings solo con propiedades básicas, pero servicio espera más
**Archivo:** `MediaStorageService.cs:78-79, 241`, `appsettings.json:5-9`
**Repro:** Intentar subir video, verificar si `AllowVideoUpload` está definido
**Fix:**
1. Verificar `StorageSettings.cs` tiene todas las propiedades usadas
2. Añadir valores por defecto seguros
3. Documentar configuración requerida

### BUG-005: Race condition en adaptación batch
**Severidad:** MEDIA
**Síntoma:** Dos workers de Hangfire podrían procesar el mismo post simultáneamente
**Causa:** No hay bloqueo/lock al procesar posts pendientes
**Archivo:** `ContentAdaptationService.cs:37-42`
**Repro:** Tener múltiples Hangfire workers, muchos posts pendientes
**Fix:**
1. Añadir `FOR UPDATE SKIP LOCKED` en query o usar transacción con isolation level
2. O cambiar estado a "EnProceso" antes de adaptar

### BUG-006: PublishToInstagramWithRefitAsync retorna ID fake cuando no hay media
**Severidad:** BAJA (comportamiento conocido)
**Síntoma:** Instagram sin imagen genera ID ficticio `ig_no_media_{guid}`
**Causa:** Instagram requiere imagen pero el código permite continuar sin ella
**Archivo:** `SocialPublisherService.cs:364-367`
**Repro:** Crear post solo texto para Instagram
**Fix:**
1. Validar en UI que Instagram tiene media antes de permitir seleccionarlo
2. O fallar con error claro en lugar de ID fake

### BUG-007: No se valida longitud de contenido por red
**Severidad:** BAJA
**Síntoma:** X/Twitter rechaza tweets > 280 caracteres; la IA podría generar más
**Causa:** `AiContentService.ApplyBasicRules` trunca, pero `AdaptContentAsync` con IA no garantiza límite
**Archivo:** `AiContentService.cs:274-287`
**Repro:** Contenido largo, desactivar IA, publicar en X
**Fix:**
1. Validar y truncar después de recibir respuesta de IA
2. Añadir validación en Preview.razor antes de publicar

---

## 5. TOP ANTIPATRONES Y CODE SMELLS

### 5.1 God Service: SocialPublisherService (~640 líneas)
**Ubicación:** `SocialPanelCore.Infrastructure/Services/SocialPublisherService.cs`
**Impacto:** Difícil de mantener, testear y extender
**Evidencia:** Un servicio maneja 6 redes sociales distintas con lógica específica para cada una
**Recomendación:** Extraer a Strategy Pattern con `INetworkPublisher` por red:
```csharp
public interface INetworkPublisher
{
    NetworkType SupportedNetwork { get; }
    Task<string> PublishAsync(AdaptedPost post, ChannelCredentials credentials);
}
```

### 5.2 Lógica de negocio en componentes Razor
**Ubicación:** `Components/Pages/Publications/New.razor:349-418`, `Preview.razor:212-255`
**Impacto:** No reutilizable, difícil de testear
**Evidencia:** `SaveAsDraft()` y `SaveAndSchedule()` contienen lógica de creación de posts
**Recomendación:** Mover a Application Services o usar Mediator pattern

### 5.3 Sin abstracción para HttpClient calls
**Ubicación:** `OAuthService.cs`, `SocialPublisherService.cs:564-622` (LinkedIn)
**Impacto:** Código duplicado, difícil de mockear en tests
**Evidencia:** LinkedIn usa HttpClient directamente mientras otras redes usan Refit
**Recomendación:** Migrar LinkedIn a Refit client también

### 5.4 Manejo inconsistente de excepciones
**Ubicación:** `AiContentService.cs:67-80`, `ContentAdaptationService.cs:48-56`
**Impacto:** Errores silenciosos, pérdida de información
**Evidencia:** Catch genérico `Exception ex` que solo loguea y retorna fallback
**Recomendación:** Definir excepciones de dominio específicas, propagar cuando sea apropiado

### 5.5 Propiedades computadas en entidades (posible N+1)
**Ubicación:** `PostMedia.cs:67-79` (Url, IsVideo, IsImage)
**Impacto:** Funciona bien, pero podría confundir en queries EF
**Evidencia:** Propiedades sin `[NotMapped]` explícito (aunque EF las ignora por ser get-only)
**Recomendación:** Añadir `[NotMapped]` para claridad o mover a DTO

### 5.6 Configuración hardcodeada
**Ubicación:** `AiContentService.cs:161-208` (prompts por red)
**Impacto:** Cambios requieren rebuild
**Evidencia:** Directrices de cada red social están en código
**Recomendación:** Mover a configuración externa o BD para poder actualizar sin deploy

### 5.7 Tests inexistentes
**Ubicación:** No hay carpeta `*.Tests` ni archivos `*Test*.cs`
**Impacto:** Riesgo alto de regresiones, refactoring peligroso
**Evidencia:** `Glob(**/*Test*.cs)` retorna vacío
**Recomendación:** Priorizar tests para servicios críticos: `BasePostService`, `SocialPublisherService`

---

## 6. SEGURIDAD, RENDIMIENTO Y OPERABILIDAD

### 6.1 Seguridad

| Check | Estado | Evidencia | Riesgo |
|-------|--------|-----------|--------|
| Authn | OK | ASP.NET Core Identity con cookies | - |
| Authz (roles) | PARCIAL | `[Authorize]` en páginas pero no granular por cuenta | Cualquier usuario puede ver todas las cuentas |
| Secretos en código | FALLO | `appsettings.json` con Password BD, API keys | CRÍTICO |
| Tokens cifrados | OK | DataProtection API en `SocialChannelConfigService` | - |
| CORS | N/A | Blazor Server no expone API REST | - |
| CSRF | OK | `UseAntiforgery()` en Program.cs:191 | - |
| SQL Injection | OK | EF Core parameterizado | - |
| XSS | OK | Blazor escapa por defecto | - |
| Hangfire Dashboard | PARCIAL | `HangfireAuthorizationFilter` existe pero revisar implementación | Verificar que requiere auth |

### 6.2 Rendimiento

| Check | Estado | Ubicación | Riesgo |
|-------|--------|-----------|--------|
| Queries N+1 | POSIBLE | `GetPostsByAccountAsync` incluye `TargetNetworks`, `CreatedByUser` | Revisar con profiler |
| Paginación | NO | `Publications/Index.razor` carga todos los posts | Problemas con muchos posts |
| Índices BD | PARCIAL | Índice en `(AccountId, NetworkType)`, falta en `ScheduledAtUtc` | Query lento en Hangfire job |
| Caching | NO | No hay caching de cuentas, configuraciones | Cada request a BD |
| Rate Limiting APIs | NO | Sin Polly rate limits en Refit clients | Posible ban de APIs |

### 6.3 Operabilidad

| Check | Estado | Ubicación | Notas |
|-------|--------|-----------|-------|
| Logging | OK | Serilog Console + File, 30 días retención | `appsettings.json:50-78` |
| Health checks | NO | No hay `/health` endpoint | Añadir para K8s/Docker |
| Métricas | NO | No hay Prometheus/OpenTelemetry | - |
| Timeouts | NO | HttpClient sin timeout explícito | Requests pueden colgar |
| Retry policies | PARCIAL | Polly referenciado pero no configurado en Refit | Solo referencia en .csproj |

### 6.4 CI/CD

| Check | Estado | Notas |
|-------|--------|-------|
| GitHub Actions | NO | No hay `.github/workflows/` |
| Linters | NO | No hay `.editorconfig` ni análisis estático |
| Tests en pipeline | N/A | No hay tests |
| Docker | NO | No hay Dockerfile |

---

## 7. PLAN DE ACCIÓN POR FASES

### FASE 1: Quick Wins (1-2 días cada una)

| # | Acción | Impacto | Dependencia |
|---|--------|---------|-------------|
| 1.1 | **Mover secretos a User Secrets/ENV** | Seguridad CRÍTICA | Ninguna |
| 1.2 | **Añadir índice en BasePosts(ScheduledAtUtc)** | Performance Hangfire | Ninguna |
| 1.3 | **Implementar paginación en Publications/Index** | UX + Performance | Ninguna |
| 1.4 | **Añadir [NotMapped] a propiedades computadas** | Claridad código | Ninguna |
| 1.5 | **Validar longitud contenido antes de publicar** | Evitar errores X/TikTok | Ninguna |

### FASE 2: Estabilización (1-2 semanas)

| # | Acción | Impacto | Dependencia |
|---|--------|---------|-------------|
| 2.1 | **Implementar refresh automático de tokens** | Publicación confiable | 1.1 |
| 2.2 | **Añadir tests unitarios para servicios core** | Confiabilidad | Ninguna |
| 2.3 | **Extraer publishers a Strategy Pattern** | Mantenibilidad | 2.2 |
| 2.4 | **Configurar Polly retry/circuit breaker en Refit** | Resiliencia | Ninguna |
| 2.5 | **Añadir health checks endpoint** | Operabilidad | Ninguna |
| 2.6 | **Migrar LinkedIn a Refit client** | Consistencia | 2.3 |

### FASE 3: Completar funcionalidades (1+ mes)

| # | Acción | Impacto | Dependencia |
|---|--------|---------|-------------|
| 3.1 | **Implementar permisos por cuenta (authz granular)** | Seguridad multi-tenant | 2.2 |
| 3.2 | **Añadir calendario visual (FullCalendar o similar)** | UX | Ninguna |
| 3.3 | **Implementar almacenamiento cloud (S3/Azure Blob)** | Escalabilidad medios | Ninguna |
| 3.4 | **Completar flujo de revisiones** | Proceso aprobación | Ninguna |
| 3.5 | **Añadir soporte videos (YouTube/TikTok)** | Funcionalidad completa | 3.3 |
| 3.6 | **Configurar CI/CD con GitHub Actions** | Automatización | 2.2 |
| 3.7 | **Añadir métricas y trazas (OpenTelemetry)** | Observabilidad | 2.5 |

### Orden recomendado de ejecución:
```
1.1 → 2.1 → 2.2 → 2.3 → 2.4
     ↓
1.2 → (paralelo con resto)
     ↓
1.3 → 3.2
     ↓
1.4, 1.5 → 2.6
     ↓
2.5 → 3.7
     ↓
3.1 → 3.4
     ↓
3.3 → 3.5
     ↓
3.6 (después de 2.2)
```

---

## 8. CONCLUSIONES

**Fortalezas del proyecto:**
- Arquitectura Clean bien definida
- Integración completa con 6 redes sociales
- Sistema de IA para adaptación de contenido funcional
- Cifrado de tokens implementado
- Logging estructurado con Serilog

**Debilidades principales:**
- Sin tests automatizados (riesgo alto)
- Secretos en repositorio
- Sin refresh automático de OAuth tokens
- Falta authorización granular por cuenta
- Sin CI/CD

**Recomendación final:**
El proyecto está en estado funcional para demos/MVP pero **NO está listo para producción**. Las prioridades inmediatas deben ser:
1. Sacar secretos del código
2. Añadir tests mínimos para servicios críticos
3. Implementar refresh de tokens

Con esas 3 acciones completadas, el sistema puede considerarse estable para un piloto limitado.

---

*Reporte generado automáticamente - Auditoría de código legacy*
