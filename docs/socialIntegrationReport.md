# Social Integration Report - Documento Principal

**Proyecto:** SocialPanelCore
**Fecha:** 12/12/2025
**Versión:** 1.0

---

## Tabla de Contenidos

1. [Resumen Ejecutivo](#1-resumen-ejecutivo)
2. [Estado Actual del Sistema](#2-estado-actual-del-sistema)
3. [Problemas e Incoherencias Detectadas](#3-problemas-e-incoherencias-detectadas)
4. [Requerimientos Nuevos](#4-requerimientos-nuevos)
5. [Arquitectura Propuesta](#5-arquitectura-propuesta)
6. [Plan de Sprints](#6-plan-de-sprints)
7. [Decisiones Técnicas](#7-decisiones-técnicas)

---

## 1. Resumen Ejecutivo

### 1.1 Objetivo del Proyecto

SocialPanelCore es una aplicación para gestionar publicaciones en múltiples redes sociales (X/Twitter, Instagram, Facebook, YouTube, TikTok, LinkedIn). Permite:

- Crear publicaciones con contenido original
- Adaptar automáticamente el contenido para cada red social usando IA
- Programar publicaciones para fechas futuras
- Publicar inmediatamente
- Gestionar múltiples cuentas/tenants

### 1.2 Stack Tecnológico Actual

| Componente | Tecnología |
|------------|------------|
| Framework | .NET 10.0 |
| UI | Blazor Server + MudBlazor 8.15.0 |
| Base de Datos | PostgreSQL |
| ORM | Entity Framework Core |
| Jobs en Background | Hangfire 1.8.22 |
| Autenticación | ASP.NET Core Identity |
| Logging | Serilog |

### 1.3 Alcance de Este Documento

Este documento describe:
- El análisis completo del estado actual
- Los problemas encontrados
- Los nuevos requerimientos
- La arquitectura propuesta
- El plan de implementación dividido en 5 sprints

---

## 2. Estado Actual del Sistema

### 2.1 Arquitectura de Capas

```
SocialPanelCore/
├── SocialPanelCore.Domain/           # Capa de Dominio
│   ├── Entities/                     # Modelos de negocio
│   ├── Enums/                        # Enumeraciones
│   └── Interfaces/                   # Contratos de servicios
├── SocialPanelCore.Infrastructure/   # Capa de Infraestructura
│   ├── Data/                         # DbContext
│   └── Services/                     # Implementaciones
├── Components/                       # Capa de Presentación (Blazor)
│   ├── Pages/                        # Páginas Razor
│   ├── Shared/                       # Componentes compartidos
│   └── Layout/                       # Layouts
└── Program.cs                        # Configuración DI
```

### 2.2 Modelos de Dominio Existentes

#### BasePost (Publicación Base)
```
Ubicación: SocialPanelCore.Domain/Entities/BasePost.cs

Campos actuales:
- Id: Guid
- AccountId: Guid (cuenta/tenant propietario)
- CreatedByUserId: Guid?
- Title: string?
- Content: string (contenido original)
- ScheduledAtUtc: DateTime (fecha programada)
- State: BasePostState (enum de estados)
- ContentType: ContentType (FeedPost, Story, Reel)
- RequiresApproval: bool
- Campos de aprobación/rechazo
- PublishedAt: DateTime?
- CreatedAt, UpdatedAt: DateTime

Navegación:
- Account
- CreatedByUser
- TargetNetworks: ICollection<PostTargetNetwork>
- AdaptedVersions: ICollection<AdaptedPost>
```

#### PostTargetNetwork (Red Objetivo)
```
Ubicación: SocialPanelCore.Domain/Entities/PostTargetNetwork.cs

Campos actuales:
- Id: Guid
- BasePostId: Guid
- NetworkType: NetworkType (enum)

Navegación:
- BasePost
```

#### AdaptedPost (Versión Adaptada)
```
Ubicación: SocialPanelCore.Domain/Entities/AdaptedPost.cs

Campos actuales:
- Id: Guid
- BasePostId: Guid
- NetworkType: NetworkType
- AdaptedContent: string
- CharacterCount: int
- State: AdaptedPostState (Pending, Ready, Published, Failed)
- PublishedAt: DateTime?
- ExternalPostId: string?
- LastError: string?
- RetryCount: int
- CreatedAt: DateTime

Navegación:
- BasePost
```

#### SocialChannelConfig (Configuración de Red Social)
```
Ubicación: SocialPanelCore.Domain/Entities/SocialChannelConfig.cs

Campos actuales:
- Id: Guid
- AccountId: Guid
- NetworkType: NetworkType
- AuthMethod: AuthMethod (OAuth o ApiKey)
- Campos OAuth (AccessToken, RefreshToken, TokenExpiresAt)
- Campos ApiKey (ApiKey, ApiSecret, AccessTokenSecret)
- Handle: string?
- ExternalUserId: string?
- IsEnabled: bool
- HealthStatus: HealthStatus
- LastHealthCheck, LastErrorMessage
- CreatedAt, UpdatedAt

Navegación:
- Account
```

### 2.3 Enumeraciones Existentes

```csharp
// BasePostState
Borrador = 0,
Planificada = 1,
AdaptacionPendiente = 2,
Adaptada = 3,
ParcialmentePublicada = 4,
Publicada = 5,
Cancelada = 6

// AdaptedPostState
Pending = 0,
Ready = 1,
Published = 2,
Failed = 3

// NetworkType
Facebook = 0,
Instagram = 1,
TikTok = 2,
X = 3,
YouTube = 4,
LinkedIn = 5

// ContentType
FeedPost = 0,
Story = 1,
Reel = 2
```

### 2.4 Servicios Existentes

| Servicio | Responsabilidad |
|----------|-----------------|
| `BasePostService` | CRUD de publicaciones, gestión de estados |
| `ContentAdaptationService` | Adaptación de contenido (placeholder, no usa IA real) |
| `SocialPublisherService` | Publicación en redes sociales |
| `SocialChannelConfigService` | Gestión de credenciales |
| `OAuthService` | Flujos OAuth |
| `AccountService` | Gestión de cuentas/tenants |
| `UserService` | Gestión de usuarios |

### 2.5 Páginas UI Existentes

| Ruta | Archivo | Estado |
|------|---------|--------|
| `/publications` | `Index.razor` | ✅ Funcional |
| `/publications/new` | `New.razor` | ✅ Funcional (parcial) |
| `/publications/view/{id}` | `View.razor` | ❌ NO EXISTE |
| `/publications/edit/{id}` | `Edit.razor` | ❌ NO EXISTE |

### 2.6 Jobs de Hangfire

```csharp
// Cada 3 horas: Adaptar contenido pendiente
RecurringJob.AddOrUpdate<IContentAdaptationService>(
    "adaptar-contenido-ia",
    service => service.AdaptPendingPostsAsync(),
    "0 */3 * * *");

// Cada 5 minutos: Publicar posts programados
RecurringJob.AddOrUpdate<ISocialPublisherService>(
    "publicar-posts-programados",
    service => service.PublishScheduledPostsAsync(),
    "*/5 * * * *");
```

---

## 3. Problemas e Incoherencias Detectadas

### 3.1 CRÍTICO: Páginas View y Edit No Implementadas

**Problema:** Las rutas `/publications/view/{id}` y `/publications/edit/{id}` se referencian en `Index.razor` pero los archivos no existen.

**Impacto:** El usuario no puede ver ni editar publicaciones existentes (error 404).

**Archivos afectados:**
- `Components/Pages/Publications/Index.razor` (líneas 271, 276)

```csharp
// Index.razor - Código que referencia páginas inexistentes
private void ViewPublication(BasePost post)
{
    Navigation.NavigateTo($"/publications/view/{post.Id}");  // ❌ No existe
}

private void EditPublication(BasePost post)
{
    Navigation.NavigateTo($"/publications/edit/{post.Id}");  // ❌ No existe
}
```

---

### 3.2 CRÍTICO: Archivos Subidos No Se Almacenan

**Problema:** El formulario `New.razor` permite subir archivos pero estos NO se guardan en ningún lugar.

**Código actual (New.razor líneas 296-309):**
```csharp
private List<IBrowserFile> _uploadedFiles = new();

private Task OnFilesSelected(InputFileChangeEventArgs e)
{
    _uploadedFiles.Clear();
    foreach (var file in e.GetMultipleFiles(10))
    {
        _uploadedFiles.Add(file);  // Solo en memoria, se pierde al guardar
    }
    return Task.CompletedTask;
}
```

**Impacto:** Los usuarios creen que suben archivos pero estos se pierden completamente.

**Causa raíz:**
1. No existe modelo `PostMedia` para almacenar metadatos de archivos
2. No existe servicio de almacenamiento de archivos
3. `BasePost` no tiene relación con medios

---

### 3.3 CRÍTICO: No Existe Configuración de AI Optimization

**Problema:** No hay forma de indicar si una publicación debe ser optimizada por IA o publicarse "en bruto".

**Estado actual:**
- Todas las publicaciones pasan obligatoriamente por el flujo de adaptación IA
- No existe campo `AiOptimizationEnabled` en `BasePost`
- No existe campo para control individual por red en `PostTargetNetwork`

**Impacto:** No se puede publicar contenido sin procesar por IA.

---

### 3.4 CRÍTICO: "Publicar Ahora" No Funciona Correctamente

**Problema:** Cuando el usuario marca "Publicar inmediatamente", la publicación queda en estado `Planificada` con `ScheduledAtUtc = DateTime.UtcNow`, pero depende de:
1. Hangfire adapte el contenido (cada 3 horas)
2. Hangfire publique (cada 5 minutos)

**Código actual (New.razor líneas 384-396):**
```csharp
if (_model.ScheduleNow)
{
    scheduledDate = DateTime.UtcNow;  // Se guarda con fecha actual
}
// ... pero sigue dependiendo de Hangfire
```

**Impacto:** El usuario espera publicación inmediata pero puede tardar hasta 3+ horas.

---

### 3.5 ALTO: ContentAdaptationService Es Un Placeholder

**Problema:** El servicio de adaptación no usa IA real, solo aplica reglas básicas hardcodeadas.

**Código actual (ContentAdaptationService.cs líneas 111-136):**
```csharp
private async Task<string> GenerateAdaptedContentAsync(BasePost post, NetworkType network)
{
    // TODO: Integrar con servicio de IA real  ← NUNCA IMPLEMENTADO

    var adapted = network switch
    {
        NetworkType.X => TruncateWithEllipsis(content, 280),  // Solo trunca
        NetworkType.LinkedIn => $"{content}\n\n#profesional #negocios",  // Solo añade hashtags
        // ... reglas básicas
    };

    await Task.Delay(100);  // Simula latencia
    return adapted;
}
```

**Impacto:** La "optimización por IA" es falsa, solo aplica transformaciones triviales.

---

### 3.6 ALTO: No Hay Configuración de Medios por Red Social

**Problema:** `SocialChannelConfig` no tiene campo para indicar si la red permite/prefiere publicar con medios.

**Impacto:** No se puede configurar que X no publique imágenes (por coste de API) mientras Instagram sí.

---

### 3.7 MEDIO: Instagram/TikTok/YouTube Requieren Media

**Problema:** El código actual detecta que estas redes requieren media pero no lo maneja correctamente.

**Código actual (SocialPublisherService.cs):**
```csharp
// Instagram (línea 394)
_logger.LogWarning("Instagram requiere imagen/video. Publicacion de solo texto no soportada.");
return $"ig_text_only_{Guid.NewGuid():N}";  // Devuelve ID falso

// TikTok (línea 473)
return $"tt_video_required_{Guid.NewGuid():N}";  // Devuelve ID falso

// YouTube (línea 487)
return $"yt_video_required_{Guid.NewGuid():N}";  // Devuelve ID falso
```

**Impacto:** Se registran como "publicados" posts que en realidad nunca se publicaron.

---

### 3.8 MEDIO: No Hay Vista Previa de Adaptaciones

**Problema:** El usuario no puede ver cómo quedará el contenido adaptado antes de publicar.

**Impacto:** Experiencia de usuario pobre, no hay control sobre el resultado final.

---

### 3.9 BAJO: Zona Horaria No Gestionada en UI

**Problema:** Las fechas se guardan en UTC pero la UI no convierte a zona horaria local.

**Código actual (New.razor línea 391):**
```csharp
scheduledDate = _model.ScheduledDate.Value.Date + _model.ScheduledTime.Value;
// No se especifica zona horaria del usuario
```

---

### 3.10 BAJO: Límite Arbitrario de Cuentas

**Problema:** Al cargar publicaciones, se limita a 10 cuentas sin razón clara.

**Código (Index.razor línea 176):**
```csharp
foreach (var account in _accounts.Take(10)) // Limitar para demo
```

---

## 4. Requerimientos Nuevos

### 4.1 Sistema de AI Optimization Configurable

**Descripción:**
El usuario debe poder decidir si cada publicación (o cada red específica) se optimiza con IA o se publica "en bruto".

**Comportamiento:**
1. Checkbox global "Optimizar con IA" a nivel de publicación
2. Al marcar/desmarcar, selecciona/deselecciona todos los checkboxes de redes
3. Cada red social tiene su propio checkbox independiente
4. Si una red tiene `AiOptimization = false`, el contenido se publica tal cual

**Modelo de datos necesario:**
```csharp
// BasePost - añadir
bool AiOptimizationEnabled { get; set; }

// PostTargetNetwork - añadir
bool UseAiOptimization { get; set; }
```

---

### 4.2 Sistema de Medios/Archivos

**Descripción:**
Las publicaciones pueden incluir imágenes que se almacenan en el servidor.

**Estructura de carpetas:**
```
/uploads/
└── {NombreCuenta}/              # Ej: peluqueriaspaco
    └── {RedSocial}/             # Ej: instagram
        └── {Titulo}_{Fecha}.jpg # Ej: publicacion_navidad_24122025.jpg
```

**Formatos permitidos:** PNG, JPG, JPEG

**Configuración por red:**
- Campo `AllowMedia: bool` en `SocialChannelConfig`
- Campo `IncludeMedia: bool` en `PostTargetNetwork` (si el BasePost tiene medios)

---

### 4.3 Dos Modos de Publicación

#### 4.3.1 Publicación Programada

**Sin AI Optimization:**
```
Borrador → Planificada → Publicada
```

**Con AI Optimization:**
```
Borrador → Planificada → AdaptacionPendiente → Adaptada → Publicada
```

#### 4.3.2 Publicación Inmediata

**Sin AI Optimization:**
```
(crear) → Publicada (inmediato, sin Hangfire)
```

**Con AI Optimization:**
```
(crear) → AdaptacionPendiente → [espera síncrona] → Adaptada → [preview editable] → Publicada
```

---

### 4.4 Vista Previa y Edición de Adaptaciones

**Descripción:**
Antes de confirmar la publicación, el usuario puede:
1. Ver cómo queda el contenido adaptado para cada red
2. Editar manualmente cada adaptación
3. Confirmar o cancelar

---

### 4.5 Integración Real con APIs de Redes Sociales

**Tecnología:** Refit (excepto YouTube que usa SDK oficial)

| Red | Librería | Base URL |
|-----|----------|----------|
| YouTube | `Google.Apis.YouTube.v3` | SDK oficial |
| X (Twitter) | Refit | `https://api.x.com` |
| Instagram | Refit | `https://graph.facebook.com/v18.0` |
| Facebook | Refit | `https://graph.facebook.com/v18.0` |
| TikTok | Refit | `https://open.tiktokapis.com` |

---

## 5. Arquitectura Propuesta

### 5.1 Nuevos Modelos de Dominio

#### PostMedia (NUEVO)
```csharp
public class PostMedia
{
    public Guid Id { get; set; }
    public Guid BasePostId { get; set; }
    public string FileName { get; set; }        // nombre_original.jpg
    public string StoredFileName { get; set; }  // guid.jpg
    public string FilePath { get; set; }        // ruta completa en servidor
    public string ContentType { get; set; }     // image/jpeg
    public long FileSize { get; set; }          // bytes
    public int SortOrder { get; set; }          // orden de las imágenes
    public DateTime CreatedAt { get; set; }

    // Navegación
    public virtual BasePost BasePost { get; set; }
}
```

#### Modificaciones a BasePost
```csharp
public class BasePost
{
    // ... campos existentes ...

    // NUEVOS CAMPOS
    public bool AiOptimizationEnabled { get; set; } = true;
    public PublishMode PublishMode { get; set; }  // Scheduled o Immediate

    // NUEVA NAVEGACIÓN
    public virtual ICollection<PostMedia> Media { get; set; }
}
```

#### Modificaciones a PostTargetNetwork
```csharp
public class PostTargetNetwork
{
    // ... campos existentes ...

    // NUEVOS CAMPOS
    public bool UseAiOptimization { get; set; } = true;
    public bool IncludeMedia { get; set; } = true;
}
```

#### Modificaciones a SocialChannelConfig
```csharp
public class SocialChannelConfig
{
    // ... campos existentes ...

    // NUEVO CAMPO
    public bool AllowMedia { get; set; } = true;
}
```

#### Nuevo Enum PublishMode
```csharp
public enum PublishMode
{
    Scheduled = 0,   // Programada (usa Hangfire)
    Immediate = 1    // Inmediata (síncrona)
}
```

### 5.2 Nuevos Servicios

#### IMediaStorageService
```csharp
public interface IMediaStorageService
{
    Task<PostMedia> SaveMediaAsync(Guid basePostId, IBrowserFile file, string accountName, NetworkType network, string postTitle);
    Task<IEnumerable<PostMedia>> GetMediaByPostIdAsync(Guid basePostId);
    Task DeleteMediaAsync(Guid mediaId);
    Task DeleteAllMediaByPostIdAsync(Guid basePostId);
    string GetMediaUrl(PostMedia media);
}
```

#### IAiContentService (reemplaza ContentAdaptationService)
```csharp
public interface IAiContentService
{
    Task<string> AdaptContentAsync(string originalContent, NetworkType network, string? accountContext = null);
    Task<Dictionary<NetworkType, string>> AdaptContentForNetworksAsync(string originalContent, List<NetworkType> networks);
    Task<AdaptedPost> CreateAdaptedPostAsync(Guid basePostId, NetworkType network, bool useAi = true);
}
```

#### IImmediatePublishService (NUEVO)
```csharp
public interface IImmediatePublishService
{
    Task<PublishResult> PublishImmediatelyAsync(Guid basePostId);
    Task<Dictionary<NetworkType, string>> GeneratePreviewsAsync(Guid basePostId);
    Task<PublishResult> PublishAfterPreviewAsync(Guid basePostId, Dictionary<NetworkType, string> editedContent);
}
```

### 5.3 Interfaces Refit para APIs Externas

```csharp
// X (Twitter)
public interface IXApiClient
{
    [Post("/2/tweets")]
    Task<XTweetResponse> CreateTweetAsync([Body] XTweetRequest request);

    [Delete("/2/tweets/{id}")]
    Task DeleteTweetAsync(string id);

    [Post("/2/media/upload")]
    Task<XMediaUploadResponse> UploadMediaAsync([Body] MultipartFormDataContent content);
}

// Meta (Facebook/Instagram)
public interface IMetaGraphApiClient
{
    [Post("/{pageId}/feed")]
    Task<MetaPostResponse> CreateFacebookPostAsync(string pageId, [Body] MetaPostRequest request);

    [Post("/{igUserId}/media")]
    Task<MetaMediaContainerResponse> CreateInstagramContainerAsync(string igUserId, [Body] InstagramMediaRequest request);

    [Post("/{igUserId}/media_publish")]
    Task<MetaPostResponse> PublishInstagramMediaAsync(string igUserId, [Query] string creation_id);
}

// TikTok
public interface ITikTokApiClient
{
    [Post("/v2/post/publish/content/init/")]
    Task<TikTokInitResponse> InitPublishAsync([Body] TikTokPublishRequest request);

    [Post("/v2/post/publish/status/fetch/")]
    Task<TikTokStatusResponse> GetPublishStatusAsync([Body] TikTokStatusRequest request);
}
```

### 5.4 Diagrama de Flujo Propuesto

```
┌─────────────────────────────────────────────────────────────────────────┐
│                        CREAR NUEVA PUBLICACIÓN                          │
└─────────────────────────────────────────────────────────────────────────┘
                                    │
                                    ▼
                    ┌───────────────────────────────┐
                    │  Formulario New.razor         │
                    │  - Título, Contenido          │
                    │  - Selección de redes         │
                    │  - Checkbox AI global         │
                    │  - Checkboxes AI por red      │
                    │  - Upload de medios           │
                    │  - Fecha/hora o "Ahora"       │
                    └───────────────────────────────┘
                                    │
                                    ▼
                    ┌───────────────────────────────┐
                    │  ¿Publicar inmediatamente?    │
                    └───────────────────────────────┘
                          │                │
                         SÍ               NO
                          │                │
                          ▼                ▼
            ┌──────────────────┐  ┌──────────────────┐
            │ FLUJO INMEDIATO  │  │ FLUJO PROGRAMADO │
            └──────────────────┘  └──────────────────┘
                    │                      │
                    ▼                      ▼
        ┌───────────────────┐    ┌───────────────────┐
        │ ¿AI Optimization? │    │ Guardar como      │
        └───────────────────┘    │ "Planificada"     │
              │        │         │ (Hangfire lo      │
             SÍ       NO         │  procesará)       │
              │        │         └───────────────────┘
              ▼        ▼
    ┌──────────────┐ ┌──────────────┐
    │ Adaptar      │ │ Publicar     │
    │ contenido    │ │ directamente │
    │ (síncrono)   │ │ (sin IA)     │
    └──────────────┘ └──────────────┘
            │               │
            ▼               │
    ┌──────────────┐        │
    │ Mostrar      │        │
    │ Preview      │        │
    │ (editable)   │        │
    └──────────────┘        │
            │               │
            ▼               │
    ┌──────────────┐        │
    │ ¿Confirmar?  │        │
    └──────────────┘        │
       │        │           │
      SÍ       NO           │
       │        │           │
       ▼        ▼           │
    ┌──────┐ ┌──────┐       │
    │Publi-│ │Cance-│       │
    │car   │ │lar   │       │
    └──────┘ └──────┘       │
       │                    │
       ▼                    ▼
    ┌───────────────────────────┐
    │    PUBLICAR EN REDES      │
    │  (SocialPublisherService) │
    └───────────────────────────┘
```

---

## 6. Plan de Sprints

### Sprint 1: Fundamentos (Modelos y Migraciones)
**Documento:** `docs/sprint1-fundamentos.md`
- Crear modelo `PostMedia`
- Modificar `BasePost`, `PostTargetNetwork`, `SocialChannelConfig`
- Crear enum `PublishMode`
- Generar migraciones de BD
- **Duración estimada:** 2-3 días

### Sprint 2: Páginas View y Edit
**Documento:** `docs/sprint2-view-edit.md`
- Crear página `View.razor`
- Crear página `Edit.razor`
- Actualizar `Index.razor`
- **Duración estimada:** 3-4 días

### Sprint 3: Sistema de Medios
**Documento:** `docs/sprint3-medios.md`
- Implementar `MediaStorageService`
- Modificar `New.razor` para guardar archivos
- Mostrar medios en `View.razor` y `Edit.razor`
- **Duración estimada:** 3-4 días

### Sprint 4: AI Optimization y Flujos
**Documento:** `docs/sprint4-ai-flujos.md`
- Implementar `IAiContentService` con OpenRouter
- Implementar `ImmediatePublishService`
- Crear página de Preview
- Modificar flujos de estado
- **Duración estimada:** 5-7 días

### Sprint 5: Integración APIs (Refit)
**Documento:** `docs/sprint5-apis-refit.md`
- Configurar Refit
- Implementar clientes para cada red
- Integrar subida de medios
- **Duración estimada:** 7-10 días

---

## 7. Decisiones Técnicas

### 7.1 ¿Por qué Refit en lugar de HttpClient directo?

**Ventajas:**
1. Código más limpio y tipado
2. Interfaces declarativas
3. Integración con `HttpClientFactory`
4. Manejo automático de serialización
5. Soporte para interceptores (auth, logging)

**Ejemplo comparativo:**

```csharp
// Sin Refit (actual)
using var client = _httpClientFactory.CreateClient();
var request = new HttpRequestMessage(HttpMethod.Post, "https://api.x.com/2/tweets");
request.Headers.Add("Authorization", authHeader);
request.Content = new StringContent(json, Encoding.UTF8, "application/json");
var response = await client.SendAsync(request);
var content = await response.Content.ReadAsStringAsync();
var result = JsonSerializer.Deserialize<TwitterTweetResponse>(content);

// Con Refit (propuesto)
var result = await _xApiClient.CreateTweetAsync(new XTweetRequest { Text = content });
```

### 7.2 ¿Por qué almacenamiento local para medios?

**Razones:**
1. Simplicidad para MVP
2. Sin dependencias externas (Azure, AWS)
3. Control total sobre los archivos
4. Fácil migración futura si es necesario

**Estructura propuesta:**
```
/var/www/socialpanel/uploads/
└── {tenant}/
    └── {network}/
        └── {title}_{date}_{guid}.{ext}
```

### 7.3 ¿Por qué flujo síncrono para publicación inmediata?

**Razones:**
1. Mejor UX: el usuario ve el resultado inmediatamente
2. Permite editar previews antes de publicar
3. Manejo de errores en tiempo real
4. No depende de jobs de Hangfire

**Trade-offs:**
- Request más largo (puede tardar 10-30 segundos)
- Necesita manejo de timeout
- UI debe mostrar progress

### 7.4 Configuración de OpenRouter para IA

```json
// appsettings.json
{
  "OpenRouter": {
    "ApiKey": "sk-or-v1-...",
    "Endpoint": "https://openrouter.ai/api/v1/chat/completions",
    "ModelId": "anthropic/claude-3-haiku",
    "Temperature": 0.7,
    "MaxTokens": 500
  }
}
```

---

## Próximos Pasos

1. **Revisar este documento** con el equipo
2. **Comenzar Sprint 1** - Crear los documentos detallados de cada sprint
3. **Asignar tareas** a los desarrolladores

---

**Documentos relacionados:**
- `docs/sprint1-fundamentos.md` - Modelos y migraciones
- `docs/sprint2-view-edit.md` - Páginas View y Edit
- `docs/sprint3-medios.md` - Sistema de medios
- `docs/sprint4-ai-flujos.md` - AI y flujos de publicación
- `docs/sprint5-apis-refit.md` - Integración con APIs externas
