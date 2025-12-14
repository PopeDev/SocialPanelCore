# Análisis de Gap: Especificación vs Implementación

## SocialPanelCore - Plataforma de Automatización de Redes Sociales

**Fecha de análisis:** 2025-12-14
**Versión del proyecto:** .NET 10.0 / Blazor Server con MudBlazor
**Documentos de referencia:**
- `especificacion_automatizacion_rrss_blazor.md` (Especificación funcional V1)
- `deudatecnica.md` (Reporte de deuda técnica)
- `v1report.md` (Auditoría legacy)
- `auditui.md` (Auditoría de UI)

---

## Resumen Ejecutivo

| Categoría | Especificado | Implementado | Gap |
|-----------|--------------|--------------|-----|
| **Funcionalidades Core** | 18 | 15 | 3 parciales |
| **Entidades de Dominio** | 6 | 10 | +4 adicionales |
| **Flujos de Negocio** | 10 | 8 | 2 incompletos |
| **Integraciones API** | 6 redes | 6 redes | 0 (requieren credenciales) |
| **Procesos Background** | 2 | 6 | +4 adicionales |
| **Tests Automatizados** | Recomendado | 0% | 100% gap |

### Estado General: **85% Funcional**

El proyecto implementa la mayoría de requisitos de la especificación V1, pero tiene gaps críticos en:
1. **Refresh automático de tokens OAuth** (no implementado)
2. **Calendario visual de publicaciones** (solo vista lista)
3. **Tests automatizados** (inexistentes)
4. **Seguridad de configuración** (secretos en appsettings.json)

---

## 1. Análisis por Módulo Funcional

### 1.1 Gestión de Usuarios (Sección 4.1 de especificación)

| Requisito | Estado | Implementación | Gap/Notas |
|-----------|--------|----------------|-----------|
| Dos roles: Superadministrador y Usuario básico | ✅ COMPLETO | `UserRole` enum con ambos roles | - |
| Superadmin puede autenticarse | ✅ COMPLETO | ASP.NET Core Identity | - |
| Superadmin ve todas las Cuentas | ✅ COMPLETO | `UserService.GetAllAccountsForUserAsync()` | - |
| Superadmin crea/edita/desactiva usuarios | ✅ COMPLETO | `IUserService` con métodos CRUD | - |
| Superadmin asigna Cuentas a usuarios | ⚠️ PARCIAL | `UserAccountAccess` existe | UI incompleta para gestión |
| Usuario básico solo ve Cuentas asignadas | ✅ COMPLETO | Filtrado en servicios | - |
| Email único por usuario | ✅ COMPLETO | Validación en `UserService` | - |
| Envío email con credenciales temporales | ❌ FALTANTE | `IdentityNoOpEmailSender` stub | TODO pendiente |
| Impedir eliminar último superadmin | ❌ FALTANTE | Sin validación | Riesgo de quedarse sin admin |

**Gap total módulo: 15%**

---

### 1.2 Gestión de Cuentas/Negocios (Sección 4.3)

| Requisito | Estado | Implementación | Gap/Notas |
|-----------|--------|----------------|-----------|
| Crear Cuenta con nombre y descripción | ✅ COMPLETO | `AccountService.CreateAccountAsync()` | - |
| Editar Cuenta | ✅ COMPLETO | `AccountService.UpdateAccountAsync()` | - |
| Eliminar Cuenta | ⚠️ PARCIAL | `AccountService.DeleteAccountAsync()` | No valida posts asociados |
| Cuenta sin redes conectadas permitida | ✅ COMPLETO | Modelo lo permite | - |
| Cuenta sin usuarios asignados permitida | ✅ COMPLETO | Modelo lo permite | - |
| Nombre de Cuenta no vacío | ✅ COMPLETO | Validación en servicio | - |

**Gap total módulo: 5%**

---

### 1.3 Configuración de Redes Sociales (Sección 4.4)

| Requisito | Estado | Implementación | Gap/Notas |
|-----------|--------|----------------|-----------|
| 6 redes soportadas (Facebook, Instagram, TikTok, X, YouTube, LinkedIn) | ✅ COMPLETO | `NetworkType` enum | - |
| Atributo `IsEnabled` (activar/desactivar) | ✅ COMPLETO | `SocialChannelConfig.IsEnabled` | - |
| Atributo `HealthStatus` (OK/KO) | ✅ COMPLETO | `HealthStatus` enum | Valores adicionales: Warning, Disconnected |
| Almacenar `AccessToken` cifrado | ✅ COMPLETO | DataProtection API | - |
| Almacenar `RefreshToken` cifrado | ✅ COMPLETO | DataProtection API | - |
| Almacenar `TokenExpiresAt` | ✅ COMPLETO | Campo en entidad | - |
| Almacenar `ExternalId` y `Handle` | ✅ COMPLETO | Campos en entidad | - |
| Método `AuthMethod` (OAuth, ApiKey) | ✅ COMPLETO | `AuthMethod` enum | OAuth20, OAuth1a agregados |
| No almacenar contraseñas de redes | ✅ COMPLETO | Solo tokens | - |
| Redes con IsEnabled=false no aparecen en planificación | ✅ COMPLETO | Filtrado en UI | - |
| Verificación real de salud del canal | ⚠️ STUB | `SocialChannelConfigService:291` | TODO comentado |

**Gap total módulo: 10%**

---

### 1.4 Flujo OAuth (Sección 5.4)

| Requisito | Estado | Implementación | Gap/Notas |
|-----------|--------|----------------|-----------|
| Botón "Conectar [Red]" | ✅ COMPLETO | `ConfigureOAuthDialog.razor` | - |
| Redirección a página autorización de red | ✅ COMPLETO | `OAuthService.GetAuthorizationUrl()` | - |
| Intercambio código por token | ✅ COMPLETO | `OAuthService.ExchangeCodeAsync()` | - |
| Almacenar tokens tras OAuth exitoso | ✅ COMPLETO | `SocialChannelConfigService.UpdateTokensAsync()` | - |
| Marcar IsEnabled=true y HealthStatus=OK | ✅ COMPLETO | Actualización automática | - |
| Usuario cancela OAuth → no guardar | ✅ COMPLETO | Manejo en Callback.razor | - |
| Refresh automático de tokens expirados | ❌ CRÍTICO | No implementado | BUG-001: Tokens expiran y fallan publicaciones |
| OAuth para Facebook | ✅ COMPLETO | Graph API v18 | Credenciales demo |
| OAuth para Instagram | ✅ COMPLETO | Via Meta Graph API | Credenciales demo |
| OAuth para TikTok | ✅ COMPLETO | OAuth 2.0 PKCE | Credenciales demo |
| OAuth para X/Twitter | ✅ COMPLETO | OAuth 1.0a + 2.0 | Credenciales demo |
| OAuth para YouTube | ✅ COMPLETO | Google SDK | Credenciales demo |
| OAuth para LinkedIn | ✅ COMPLETO | OAuth 2.0 | Credenciales demo |

**Gap total módulo: 20%** (principalmente por falta de refresh automático)

---

### 1.5 Publicación Base (Sección 4.5)

| Requisito | Estado | Implementación | Gap/Notas |
|-----------|--------|----------------|-----------|
| Entidad con ID único | ✅ COMPLETO | `BasePost.Id` GUID | - |
| Referencia a Cuenta | ✅ COMPLETO | `BasePost.AccountId` | - |
| Usuario creador (auditoría) | ✅ COMPLETO | `BasePost.CreatedByUserId` | - |
| Fecha/hora programada en UTC | ✅ COMPLETO | `BasePost.ScheduledAtUtc` | - |
| Título opcional | ✅ COMPLETO | `BasePost.Title` | - |
| Contenido base | ✅ COMPLETO | `BasePost.Content` | - |
| Medios asociados | ✅ COMPLETO | `PostMedia` entidad | - |
| Lista redes objetivo | ✅ COMPLETO | `PostTargetNetwork` tabla | - |
| Solo redes IsEnabled=true seleccionables | ✅ COMPLETO | Filtrado en New.razor | - |
| Estado Borrador | ✅ COMPLETO | `BasePostState.Borrador` | - |
| Estado Planificada | ✅ COMPLETO | `BasePostState.Planificada` | - |
| Estado AdaptaciónPendiente | ✅ COMPLETO | `BasePostState.AdaptacionPendiente` | - |
| Estado Adaptada | ✅ COMPLETO | `BasePostState.Adaptada` | - |
| Estado ParcialmentePublicada | ✅ COMPLETO | `BasePostState.ParcialmentePublicada` | - |
| Estado Publicada | ✅ COMPLETO | `BasePostState.Publicada` | - |
| Estado Cancelada | ✅ COMPLETO | `BasePostState.Cancelada` | - |

**Gap total módulo: 0%**

---

### 1.6 Variantes de Publicación / Adaptación IA (Sección 4.6)

| Requisito | Estado | Implementación | Gap/Notas |
|-----------|--------|----------------|-----------|
| Entidad AdaptedPost | ✅ COMPLETO | `AdaptedPost` entity | - |
| Referencia a BasePost | ✅ COMPLETO | `AdaptedPost.BasePostId` | - |
| NetworkType redundante | ✅ COMPLETO | `AdaptedPost.NetworkType` | - |
| Contenido adaptado | ✅ COMPLETO | `AdaptedPost.AdaptedContent` | - |
| Hashtags | ⚠️ PARCIAL | Integrados en contenido | No campo separado |
| Medios adaptados | ⚠️ PARCIAL | Mismos que BasePost | V1 sin adaptación de medios |
| Estado PendienteDeGeneración | ✅ COMPLETO | `AdaptedPostState.Pending` | - |
| Estado Generada/ListaParaPublicar | ✅ COMPLETO | `AdaptedPostState.Ready` | - |
| Estado Publicada | ✅ COMPLETO | `AdaptedPostState.Published` | - |
| Estado ErrorGeneración | ⚠️ PARCIAL | `AdaptedPostState.Failed` | Un solo estado para ambos errores |
| Estado ErrorPublicación | ⚠️ PARCIAL | `AdaptedPostState.Failed` | Un solo estado para ambos errores |
| Estado Cancelada | ❌ FALTANTE | No implementado | Hereda de BasePost |
| Fecha/hora publicación real | ✅ COMPLETO | `AdaptedPost.PublishedAt` | - |
| ID externo de publicación | ✅ COMPLETO | `AdaptedPost.ExternalPostId` | - |
| Mensaje de error | ✅ COMPLETO | `AdaptedPost.LastError` | - |
| Adaptación con LLM en es-ES | ✅ COMPLETO | `AiContentService.BuildPrompt()` | OpenRouter + Kimi k2 |
| Reglas técnicas por red hardcodeadas | ✅ COMPLETO | `AiContentService` métodos | Longitudes, hashtags, tono |

**Gap total módulo: 10%**

---

### 1.7 Calendario de Publicaciones (Vista Principal - Sección 5.6)

| Requisito | Estado | Implementación | Gap/Notas |
|-----------|--------|----------------|-----------|
| Vista principal es calendario | ❌ FALTANTE | Solo vista lista | Gap significativo en UX |
| Click en fecha/hora abre formulario | ❌ FALTANTE | Botón "Nueva Publicación" | Sin interacción calendario |
| Ver publicaciones en calendario | ⚠️ PARCIAL | Lista ordenada por fecha | No es visual calendario |
| Crear publicación desde calendario | ⚠️ PARCIAL | New.razor con selector fecha | Sin calendario interactivo |

**Gap total módulo: 40%** (funcionalidad crítica incompleta)

---

### 1.8 Procesos Automáticos Background (Secciones 5.8 y 5.9)

| Requisito | Estado | Implementación | Gap/Notas |
|-----------|--------|----------------|-----------|
| Proceso adaptación IA periódico | ✅ COMPLETO | Hangfire cada 3 horas | `adaptar-contenido-ia` |
| Buscar posts Planificada/AdaptaciónPendiente | ✅ COMPLETO | Query en ContentAdaptationService | - |
| Generar variantes para redes OK | ✅ COMPLETO | `AdaptPostForNetworkAsync()` | - |
| Proceso publicación periódico | ✅ COMPLETO | Hangfire cada 5 minutos | `publicar-posts-programados` |
| Publicar variantes ListaParaPublicar | ✅ COMPLETO | `PublishScheduledPostsAsync()` | - |
| Verificar IsEnabled y HealthStatus antes publicar | ✅ COMPLETO | Validación en servicio | - |
| Actualizar estado post base según variantes | ✅ COMPLETO | Lógica en servicio | - |
| Manejo error → marcar KO canal | ✅ COMPLETO | `UpdateHealthStatusAsync()` | - |

**Procesos adicionales implementados (no en spec):**
- `refrescar-tokens-oauth` (cada 15 min) - Pero no refresca realmente
- `limpiar-estados-oauth` (cada hora)
- `verificar-salud-canales` (cada 2 horas) - Stub
- `limpiar-notificaciones-expiradas` (diario)

**Gap total módulo: 5%**

---

### 1.9 Publicación en Redes Sociales (Sección 6.4.5)

| Requisito | Estado | Implementación | Gap/Notas |
|-----------|--------|----------------|-----------|
| Interfaz ISocialPublisher por red | ⚠️ PARCIAL | Un servicio monolítico | God Service antipattern |
| Publicar en Facebook | ✅ COMPLETO | `PublishToFacebookWithRefitAsync()` | Graph API |
| Publicar en Instagram | ⚠️ PARCIAL | `PublishToInstagramWithRefitAsync()` | REQUIERE imagen siempre |
| Publicar en TikTok | ⚠️ PARCIAL | `PublishToTikTokWithRefitAsync()` | REQUIERE video |
| Publicar en X/Twitter | ✅ COMPLETO | `PublishToXWithRefitAsync()` | OAuth 1.0a |
| Publicar en YouTube | ⚠️ PARCIAL | `PublishToYouTubeWithSdkAsync()` | REQUIERE video |
| Publicar en LinkedIn | ✅ COMPLETO | HttpClient directo | Sin soporte medios |
| Usar AccessToken cifrado | ✅ COMPLETO | DataProtection unprotect | - |
| Actualizar estado variante tras publicar | ✅ COMPLETO | Estados actualizados | - |
| Guardar ID externo publicación | ✅ COMPLETO | `ExternalPostId` | - |

**Gap total módulo: 15%**

---

### 1.10 Almacenamiento de Medios (Sección 6.1)

| Requisito | Estado | Implementación | Gap/Notas |
|-----------|--------|----------------|-----------|
| Almacenamiento local (wwwroot/mediavault) | ✅ COMPLETO | `/uploads/` configurado | - |
| Subida de imágenes | ✅ COMPLETO | .jpg, .jpeg, .png | - |
| Subida de videos | ⚠️ PARCIAL | Configurable pero limitado | 10MB default |
| URLs absolutas para APIs externas | ❌ FALTANTE | URLs relativas | BUG-003: Instagram/TikTok fallan |

**Gap total módulo: 15%**

---

## 2. Requisitos Técnicos

### 2.1 Stack Tecnológico (Sección 6.1)

| Requisito | Estado | Implementación |
|-----------|--------|----------------|
| C# | ✅ | .NET 10.0 |
| Blazor Server + InteractiveServer | ✅ | MudBlazor 8.15 |
| Entity Framework Core + PostgreSQL | ✅ | Npgsql EF Core 10 |
| ASP.NET Core Identity | ✅ | Configurado |
| Hosted Services / Background | ✅ | Hangfire 1.8.22 |
| OpenRouter con Kimi k2 | ✅ | AiContentService |
| Almacenamiento local medios | ✅ | MediaStorageService |

**Gap: 0%**

---

### 2.2 Estructura de Proyectos (Sección 6.2)

| Requisito | Estado | Ubicación Real |
|-----------|--------|----------------|
| Domain (entidades, interfaces) | ✅ | `SocialPanelCore.Domain/` |
| Infrastructure (DbContext, APIs) | ✅ | `SocialPanelCore.Infrastructure/` |
| Application/UI (Blazor) | ✅ | `Components/` |

**Gap: 0%** - Clean Architecture implementada

---

### 2.3 Modelo de Datos (Sección 6.3)

| Tabla Especificada | Estado | Tabla Implementada |
|--------------------|--------|-------------------|
| Users | ✅ | `AspNetUsers` (Identity) |
| Accounts | ✅ | `Accounts` |
| UserAccounts | ✅ | `UserAccountAccess` |
| SocialChannelConfigs | ✅ | `SocialChannelConfigs` |
| BasePosts | ✅ | `BasePosts` |
| BasePostTargetNetworks | ✅ | `PostTargetNetworks` |
| PostVariants | ✅ | `AdaptedPosts` |
| BasePostMedia | ✅ | `PostMedias` |

**Tablas adicionales implementadas:**
- `OAuthStates` (para flujo OAuth)
- `Notifications` (sistema notificaciones)

**Gap: 0%** (+extensiones)

---

### 2.4 Seguridad (Sección 6.7)

| Requisito | Estado | Gap/Notas |
|-----------|--------|-----------|
| Tokens cifrados en BD | ✅ COMPLETO | DataProtection API |
| No almacenar contraseñas redes | ✅ COMPLETO | Solo tokens OAuth |
| Tokens no en URLs redirección | ✅ COMPLETO | Flujo código OAuth |
| **Secretos fuera de appsettings** | ❌ CRÍTICO | Contraseña BD y API keys expuestas |

**Gap: 25%** (issue de seguridad crítico)

---

## 3. Problemas Críticos (Bugs y Gaps de Alto Impacto)

### 3.1 Bugs Documentados

| ID | Descripción | Severidad | Archivo | Estado |
|----|-------------|-----------|---------|--------|
| BUG-001 | Tokens OAuth nunca se refrescan automáticamente | CRÍTICA | SocialPublisherService.cs | Pendiente |
| BUG-002 | Credenciales expuestas en appsettings.json | CRÍTICA | appsettings.json | Pendiente |
| BUG-003 | URLs de medios relativas, no absolutas | MEDIA | PostMedia.cs | Pendiente |
| BUG-004 | StorageSettings incompleto | MEDIA | appsettings.json | Pendiente |
| BUG-005 | Race condition en adaptación batch | MEDIA | ContentAdaptationService.cs | Pendiente |
| BUG-006 | Instagram retorna ID fake sin media | BAJA | SocialPublisherService.cs | Pendiente |
| BUG-007 | Sin validación longitud contenido por red | BAJA | AiContentService.cs | Pendiente |

---

### 3.2 Gaps de UI (de auditui.md)

| ID | Descripción | Severidad | Archivo | Estado |
|----|-------------|-----------|---------|--------|
| UI-001 | AccountDialog.razor sin @rendermode | CRÍTICA | AccountDialog.razor | Pendiente |
| UI-002 | UserDialog.razor sin @rendermode | CRÍTICA | UserDialog.razor | Pendiente |
| UI-003 | ReviewDialog.razor sin @rendermode | CRÍTICA | ReviewDialog.razor | Pendiente |
| UI-004 | ConfirmDialog.razor sin @rendermode | CRÍTICA | ConfirmDialog.razor | Pendiente |
| UI-005 | Páginas Auth en inglés y Bootstrap | ALTA | Login.razor, Register.razor | Pendiente |
| UI-006 | Sin calendario visual | ALTA | Publications/Index.razor | Pendiente |
| UI-007 | Botones Edit/Delete sin OnClick | ALTA | Publications/Index.razor:116-129 | Pendiente |

---

## 4. Matriz de Completitud por Área

```
                            SPEC    IMPL    GAP
Gestión Usuarios            [████████░░] 85%     15%
Gestión Cuentas            [█████████░] 95%      5%
Config Redes Sociales      [█████████░] 90%     10%
Flujo OAuth                [████████░░] 80%     20%
Publicación Base           [██████████] 100%     0%
Variantes/Adaptación IA    [█████████░] 90%     10%
Calendario Visual          [██████░░░░] 60%     40%
Procesos Background        [█████████░] 95%      5%
Publicación Redes          [████████░░] 85%     15%
Almacenamiento Medios      [████████░░] 85%     15%
Stack Tecnológico          [██████████] 100%     0%
Arquitectura               [██████████] 100%     0%
Modelo Datos               [██████████] 100%     0%
Seguridad Configuración    [███████░░░] 75%     25%
UI/UX                      [██████░░░░] 65%     35%
Testing                    [░░░░░░░░░░] 0%     100%
──────────────────────────────────────────────
PROMEDIO PONDERADO         [████████░░] 83%     17%
```

---

## 5. Plan de Acción Priorizado

### Fase 1: Correcciones Críticas (Inmediato)

| # | Acción | Impacto | Esfuerzo |
|---|--------|---------|----------|
| 1.1 | Mover secretos a User Secrets / ENV | Seguridad | Bajo |
| 1.2 | Agregar @rendermode a diálogos (4 archivos) | UI funcional | Muy bajo |
| 1.3 | Agregar OnClick a botones Edit/Delete | UI funcional | Bajo |
| 1.4 | Implementar refresh automático tokens | Publicación confiable | Medio |

### Fase 2: Estabilización (1-2 semanas)

| # | Acción | Impacto | Esfuerzo |
|---|--------|---------|----------|
| 2.1 | Convertir URLs medios a absolutas | Instagram/TikTok funcional | Bajo |
| 2.2 | Validar último superadmin antes eliminar | Seguridad | Bajo |
| 2.3 | Agregar tests unitarios servicios core | Confiabilidad | Alto |
| 2.4 | Implementar verificación real salud canales | Monitoreo | Medio |
| 2.5 | Traducir páginas Auth a español + MudBlazor | UX | Medio |

### Fase 3: Completar Funcionalidades (1+ mes)

| # | Acción | Impacto | Esfuerzo |
|---|--------|---------|----------|
| 3.1 | Implementar calendario visual (FullCalendar) | UX crítica | Alto |
| 3.2 | Implementar envío email credenciales | Onboarding | Medio |
| 3.3 | Mejorar UI gestión Usuario ↔ Cuenta | Administración | Medio |
| 3.4 | Extraer publishers a Strategy Pattern | Mantenibilidad | Alto |
| 3.5 | Añadir rate limiting/circuit breaker APIs | Resiliencia | Medio |

---

## 6. Conclusiones

### Fortalezas del Proyecto
- ✅ Arquitectura Clean bien implementada
- ✅ Integración completa con 6 redes sociales
- ✅ Sistema de adaptación IA funcional
- ✅ Modelo de datos robusto y extensible
- ✅ Cifrado de tokens implementado
- ✅ Background jobs con Hangfire funcionando

### Debilidades Principales
- ❌ Secretos expuestos en repositorio (crítico)
- ❌ Sin refresh automático de tokens OAuth (crítico)
- ❌ Sin tests automatizados (riesgo alto)
- ❌ Diálogos no funcionan (falta @rendermode)
- ❌ Sin calendario visual (gap UX importante)
- ❌ UI Auth en inglés y Bootstrap legacy

### Recomendación Final

**Estado: MVP funcional al 85%, NO listo para producción**

El proyecto cumple con la mayoría de requisitos funcionales de la especificación V1, pero tiene gaps críticos de seguridad y estabilidad que deben resolverse antes de cualquier despliegue:

1. **Inmediato (Día 1):** Mover secretos fuera del código y arreglar diálogos
2. **Corto plazo (Semana 1):** Implementar refresh de tokens y tests básicos
3. **Mediano plazo (Mes 1):** Calendario visual y mejoras de UX

Con estas correcciones, el sistema puede considerarse listo para un piloto controlado.

---

*Documento generado: 2025-12-14*
*Próxima revisión recomendada: Tras completar Fase 1*
