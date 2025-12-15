# SocialPanelCore

**Panel de Gestión y Automatización de Publicaciones en Redes Sociales**

SocialPanelCore es una aplicación web empresarial diseñada para centralizar y automatizar la gestión de contenido en múltiples redes sociales. Permite a equipos de marketing y gestores de redes sociales crear, programar, adaptar mediante IA y publicar contenido de forma coordinada en Facebook, Instagram, X (Twitter), LinkedIn, TikTok y YouTube.

---

## Tabla de Contenidos

1. [Descripción del Proyecto](#descripción-del-proyecto)
2. [Características Principales](#características-principales)
3. [Stack Tecnológico](#stack-tecnológico)
4. [Arquitectura del Sistema](#arquitectura-del-sistema)
5. [Modelo de Dominio](#modelo-de-dominio)
6. [Flujos de Usuario](#flujos-de-usuario)
7. [Integración OAuth con Redes Sociales](#integración-oauth-con-redes-sociales)
8. [Configuración de Credenciales](#configuración-de-credenciales)
9. [Jobs en Background (Hangfire)](#jobs-en-background-hangfire)
10. [Sistema de Notificaciones](#sistema-de-notificaciones)
11. [Instalación y Configuración](#instalación-y-configuración)
12. [Estructura del Proyecto](#estructura-del-proyecto)

---

## Descripción del Proyecto

### Propósito

SocialPanelCore nace de la necesidad de las empresas y profesionales del marketing digital de gestionar múltiples cuentas y redes sociales desde una única plataforma. La aplicación permite:

- **Centralizar** la gestión de contenido para múltiples marcas/cuentas
- **Programar** publicaciones con anticipación
- **Adaptar automáticamente** el contenido para cada red social usando IA
- **Publicar simultáneamente** en múltiples plataformas
- **Monitorear** el estado de las conexiones OAuth con cada red

### Casos de Uso Principales

| Caso de Uso | Descripción |
|-------------|-------------|
| **Gestión Multi-Cuenta** | Una agencia de marketing gestiona las redes de múltiples clientes desde un solo panel |
| **Publicación Cruzada** | Crear un contenido base y publicarlo adaptado en todas las redes sociales |
| **Programación de Contenido** | Planificar semanas de contenido con fechas y horas específicas |
| **Optimización con IA** | Adaptar automáticamente el tono, longitud y hashtags según la red |
| **Flujo de Aprobación** | Equipos donde un creador redacta y un supervisor aprueba antes de publicar |

---

## Características Principales

### Redes Sociales Soportadas

| Red Social | Método de Auth | Funcionalidades |
|------------|----------------|-----------------|
| **Facebook** | OAuth 2.0 | Publicación en páginas, posts con imágenes |
| **Instagram** | OAuth 2.0 (Meta) | Publicación de imágenes (requerido), stories |
| **X (Twitter)** | OAuth 2.0 + PKCE / OAuth 1.0a | Tweets de texto e imágenes |
| **LinkedIn** | OAuth 2.0 (OpenID) | Posts profesionales |
| **TikTok** | OAuth 2.0 + PKCE | Publicación de fotos/videos |
| **YouTube** | OAuth 2.0 (Google) | Subida de videos |

### Funcionalidades Clave

- **Adaptación de Contenido con IA**: Integración con OpenRouter API para adaptar automáticamente el contenido a cada red social
- **Publicación Programada**: Jobs de Hangfire para publicar en el momento exacto
- **Publicación Inmediata**: Opción de publicar en tiempo real
- **Gestión de Medios**: Almacenamiento y gestión de imágenes/videos
- **Multi-tenancy**: Soporte para múltiples cuentas (marcas) por organización
- **Sistema de Roles**: Control de acceso por usuario y cuenta
- **Notificaciones In-App**: Alertas sobre tokens expirados, errores de publicación, etc.
- **Health Checks**: Verificación automática del estado de conexiones OAuth
- **Renovación Automática de Tokens**: Refresh automático antes de expiración

---

## Stack Tecnológico

### Framework y Runtime

| Componente | Tecnología | Versión |
|------------|------------|---------|
| **Framework** | .NET | 10.0 |
| **UI Framework** | Blazor Server | Interactive SSR |
| **Componentes UI** | MudBlazor | 8.15.0 |
| **ORM** | Entity Framework Core | 10.0.0 |
| **Base de Datos** | PostgreSQL | (via Npgsql 10.0.0) |
| **Identidad** | ASP.NET Core Identity | 10.0.0 |
| **Jobs en Background** | Hangfire | 1.8.22 |
| **Logging** | Serilog | 10.0.0 |
| **HTTP Clients** | Refit | 7.1.2 |
| **YouTube SDK** | Google.Apis.YouTube.v3 | 1.68.0 |
| **Resiliencia** | Polly | 8.6.5 |

### Librerías Principales

```xml
<!-- Core -->
<PackageReference Include="MudBlazor" Version="8.15.0" />
<PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" Version="10.0.0" />
<PackageReference Include="Microsoft.AspNetCore.Identity.EntityFrameworkCore" Version="10.0.0" />

<!-- Background Jobs -->
<PackageReference Include="Hangfire.AspNetCore" Version="1.8.22" />
<PackageReference Include="Hangfire.PostgreSql" Version="1.20.13" />

<!-- External APIs -->
<PackageReference Include="Refit" Version="7.1.2" />
<PackageReference Include="Refit.HttpClientFactory" Version="7.1.2" />
<PackageReference Include="Google.Apis.YouTube.v3" Version="1.68.0.3520" />

<!-- Observability -->
<PackageReference Include="Serilog.AspNetCore" Version="10.0.0" />
<PackageReference Include="Serilog.Sinks.Console" Version="6.1.1" />
<PackageReference Include="Serilog.Sinks.File" Version="7.0.0" />
```

---

## Arquitectura del Sistema

### Patrón: Clean Architecture

El proyecto sigue el patrón de **Clean Architecture** con tres capas bien definidas:

```
SocialPanelCore/
├── SocialPanelCore.Domain/          # Capa de Dominio (Entidades, Interfaces, Enums)
├── SocialPanelCore.Infrastructure/  # Capa de Infraestructura (Servicios, Data, APIs)
└── SocialPanelCore/                 # Capa de Presentación (Blazor, Controllers)
```

### Diagrama de Arquitectura

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                           CAPA DE PRESENTACIÓN                              │
│  ┌─────────────────┐  ┌──────────────────┐  ┌─────────────────────────────┐ │
│  │  Blazor Server  │  │  MVC Controllers │  │     Hangfire Dashboard      │ │
│  │   Components    │  │  (OAuth, Data)   │  │   /hangfire                 │ │
│  └────────┬────────┘  └────────┬─────────┘  └──────────────┬──────────────┘ │
└───────────┼────────────────────┼────────────────────────────┼───────────────┘
            │                    │                            │
┌───────────┼────────────────────┼────────────────────────────┼───────────────┐
│           │    CAPA DE APLICACIÓN / INFRAESTRUCTURA         │               │
│  ┌────────▼────────────────────▼────────────────────────────▼─────────────┐ │
│  │                         SERVICIOS                                      │ │
│  │  ┌─────────────┐ ┌────────────────┐ ┌────────────────────────────────┐ │ │
│  │  │ OAuthService│ │ ContentAdapt.  │ │   SocialPublisherService       │ │ │
│  │  │             │ │ Service        │ │   (Facebook, IG, X, LinkedIn,  │ │ │
│  │  │ TokenRefresh│ │                │ │    TikTok, YouTube)            │ │ │
│  │  │ Service     │ │ AiContentSvc   │ │                                │ │ │
│  │  └─────────────┘ └────────────────┘ └────────────────────────────────┘ │ │
│  └────────────────────────────────────────────────────────────────────────┘ │
│  ┌────────────────────────────────────────────────────────────────────────┐ │
│  │                    CLIENTES DE APIs EXTERNAS (Refit)                   │ │
│  │  ┌────────────┐  ┌──────────────┐  ┌────────────┐  ┌────────────────┐  │ │
│  │  │ IXApiClient│  │IMetaGraphApi │  │ITikTokApi  │  │YouTubeApiSvc   │  │ │
│  │  └────────────┘  └──────────────┘  └────────────┘  └────────────────┘  │ │
│  └────────────────────────────────────────────────────────────────────────┘ │
└─────────────────────────────────────────────────────────────────────────────┘
            │                    │                            │
┌───────────┼────────────────────┼────────────────────────────┼───────────────┐
│           │              CAPA DE DOMINIO                    │               │
│  ┌────────▼────────────────────▼────────────────────────────▼─────────────┐ │
│  │                         ENTIDADES                                      │ │
│  │  Account, User, BasePost, AdaptedPost, SocialChannelConfig,            │ │
│  │  PostMedia, PostTargetNetwork, OAuthState, Notification, etc.          │ │
│  └────────────────────────────────────────────────────────────────────────┘ │
└─────────────────────────────────────────────────────────────────────────────┘
            │
┌───────────▼─────────────────────────────────────────────────────────────────┐
│                         INFRAESTRUCTURA DE DATOS                            │
│  ┌────────────────────┐  ┌─────────────────┐  ┌───────────────────────────┐ │
│  │  ApplicationDbCtx  │  │   PostgreSQL    │  │   File System (Uploads)   │ │
│  │  (EF Core)         │  │   Database      │  │   /var/www/.../uploads    │ │
│  └────────────────────┘  └─────────────────┘  └───────────────────────────┘ │
└─────────────────────────────────────────────────────────────────────────────┘
```

### Patrones de Diseño Implementados

| Patrón | Uso |
|--------|-----|
| **Repository** | Acceso a datos via DbContext |
| **Service Layer** | Lógica de negocio en servicios inyectables |
| **Dependency Injection** | Inyección de dependencias nativa de ASP.NET Core |
| **Options Pattern** | Configuración tipada (StorageSettings, OAuth) |
| **Strategy** | Publicación diferenciada por tipo de red social |
| **State Pattern** | Estados de posts (Borrador, Planificada, Adaptada, etc.) |
| **Factory** | Generación de URLs OAuth por proveedor |

---

## Modelo de Dominio

### Diagrama de Entidades

```
┌─────────────────┐       ┌─────────────────┐       ┌─────────────────────┐
│     Account     │       │      User       │       │  UserAccountAccess  │
├─────────────────┤       ├─────────────────┤       ├─────────────────────┤
│ Id (Guid)       │       │ Id (Guid)       │       │ Id                  │
│ Name            │◄──────│ Name            │───────│ UserId              │
│ Description     │       │ Email           │       │ AccountId           │
│ CreatedAt       │       │ Role (enum)     │       │ CanEdit             │
│ UpdatedAt       │       │ CreatedAt       │       │ CanPublish          │
└────────┬────────┘       └─────────────────┘       │ CanApprove          │
         │                                          └─────────────────────┘
         │ 1:N
         │
┌────────▼────────┐       ┌─────────────────────┐
│SocialChannelCfg │       │     BasePost        │
├─────────────────┤       ├─────────────────────┤
│ Id              │       │ Id                  │
│ AccountId       │       │ AccountId           │
│ NetworkType     │       │ CreatedByUserId     │
│ AuthMethod      │       │ Title               │
│ AccessToken*    │       │ Content             │
│ RefreshToken*   │       │ ScheduledAtUtc      │
│ TokenExpiresAt  │       │ State (enum)        │
│ ConnectionStatus│       │ AiOptimizationEnabled│
│ HealthStatus    │       │ PublishMode (enum)  │
│ Handle          │       │ RequiresApproval    │
│ ExternalUserId  │       │ ApprovedByUserId    │
│ IsEnabled       │       │ PublishedAt         │
│ AllowMedia      │       └──────────┬──────────┘
└─────────────────┘                  │
        * cifrado                    │ 1:N
                                     │
         ┌───────────────────────────┼───────────────────────────┐
         │                           │                           │
┌────────▼────────┐       ┌──────────▼──────────┐     ┌──────────▼──────────┐
│ PostTargetNetwork│      │    AdaptedPost      │     │     PostMedia       │
├─────────────────┤       ├─────────────────────┤     ├─────────────────────┤
│ Id              │       │ Id                  │     │ Id                  │
│ BasePostId      │       │ BasePostId          │     │ BasePostId          │
│ NetworkType     │       │ NetworkType         │     │ OriginalFileName    │
│ UseAiOptimization│      │ AdaptedContent      │     │ StoredFileName      │
│ IncludeMedia    │       │ CharacterCount      │     │ RelativePath        │
└─────────────────┘       │ State (enum)        │     │ ContentType         │
                          │ PublishedAt         │     │ FileSize            │
                          │ ExternalPostId      │     │ SortOrder           │
                          │ LastError           │     │ IsVideo             │
                          │ RetryCount          │     └─────────────────────┘
                          └─────────────────────┘
```

### Enumeraciones Clave

#### UserRole - Roles de Usuario
```csharp
public enum UserRole
{
    UsuarioBasico = 0,       // Usuario estándar
    Superadministrador = 1   // Acceso completo
}
```

#### NetworkType - Redes Sociales
```csharp
public enum NetworkType
{
    Facebook = 0,
    Instagram = 1,
    TikTok = 2,
    X = 3,
    YouTube = 4,
    LinkedIn = 5
}
```

#### BasePostState - Estados de Publicación
```csharp
public enum BasePostState
{
    Borrador = 0,              // En edición
    Planificada = 1,           // Programada, pendiente de adaptación
    AdaptacionPendiente = 2,   // En cola para adaptación IA
    Adaptada = 3,              // Lista para publicar
    ParcialmentePublicada = 4, // Publicada en algunas redes
    Publicada = 5,             // Publicada en todas las redes
    Cancelada = 6              // Cancelada por el usuario
}
```

#### ConnectionStatus - Estado de Conexión OAuth
```csharp
public enum ConnectionStatus
{
    Connected = 0,     // Conexión activa
    NeedsReauth = 1,   // Requiere reconexión
    Revoked = 2,       // Usuario revocó acceso
    Error = 3,         // Error de conexión
    Pending = 4        // Configuración en proceso
}
```

#### AuthMethod - Método de Autenticación
```csharp
public enum AuthMethod
{
    OAuth = 0,   // OAuth 2.0 (Facebook, Instagram, LinkedIn, YouTube)
    ApiKey = 1   // API Keys + OAuth 1.0a (X/Twitter legacy)
}
```

---

## Flujos de Usuario

### 1. Flujo de Creación y Publicación

```
┌─────────┐     ┌─────────────┐     ┌──────────────┐     ┌─────────────┐
│ Usuario │     │  Crear Post │     │  Programar   │     │  Adaptar IA │
│ Inicia  │────►│  (Borrador) │────►│  Fecha/Hora  │────►│  (Hangfire) │
└─────────┘     └─────────────┘     └──────────────┘     └──────┬──────┘
                                                                 │
                                                                 ▼
┌─────────┐     ┌─────────────┐     ┌──────────────┐     ┌──────────────┐
│Publicado│◄────│  Publicar   │◄────│Post Adaptado │◄────│ Por cada red │
│ Exitoso │     │  (Hangfire) │     │    (Ready)   │     │   objetivo   │
└─────────┘     └─────────────┘     └──────────────┘     └──────────────┘
```

### 2. Flujo OAuth de Conexión de Red Social

```
┌─────────┐     ┌─────────────┐     ┌──────────────┐     ┌─────────────┐
│ Usuario │     │GET /oauth/  │     │  Redirige a  │     │  Usuario    │
│ Conecta │────►│connect/{red}│────►│  Proveedor   │────►│  Autoriza   │
└─────────┘     └─────────────┘     └──────────────┘     └──────┬──────┘
                                                                 │
                                                                 ▼
┌─────────┐     ┌─────────────┐     ┌──────────────┐     ┌──────────────┐
│ Conexión│◄────│  Guardar    │◄────│  Intercambio │◄────│GET /oauth/  │
│ Activa  │     │  Tokens (BD)│     │  code→tokens │     │callback/{red}│
└─────────┘     └─────────────┘     └──────────────┘     └──────────────┘
```

### 3. Flujo de Adaptación con IA

```
┌───────────────┐
│   BasePost    │
│ Content:      │
│ "Nuevo prod.  │
│  lanzamiento" │
└───────┬───────┘
        │
        │ Para cada red objetivo
        ▼
┌───────────────────────────────────────────────────────────────────────┐
│                    AiContentService.AdaptContentAsync()               │
│  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐  ┌─────────────┐  │
│  │   X (280)   │  │ Instagram   │  │  LinkedIn   │  │  Facebook   │  │
│  │ "Nuevo prod │  │ "Nuevo      │  │ "Nos complace│  │ "Gran       │  │
│  │ lanzado!"   │  │ lanzamiento │  │ anunciar..."│  │ noticia..." │  │
│  │ #tech       │  │  Descubre...│  │ #business   │  │ Opina!      │  │
│  └─────────────┘  │  #new #prod │  └─────────────┘  └─────────────┘  │
│                   │  #launch    │                                     │
│                   └─────────────┘                                     │
└───────────────────────────────────────────────────────────────────────┘
```

### 4. Ciclo de Vida de un Post

```
                    ┌──────────────────────────────────────────┐
                    │                                          │
                    ▼                                          │
┌─────────┐   ┌───────────┐   ┌────────────────┐   ┌──────────┴──┐
│Borrador │──►│Planificada│──►│ Adaptación     │──►│  Adaptada   │
│         │   │           │   │ Pendiente      │   │  (Ready)    │
└─────────┘   └───────────┘   └────────────────┘   └──────┬──────┘
      │                                                    │
      │                                                    │
      ▼                                                    ▼
┌─────────────┐                              ┌─────────────────────┐
│  Cancelada  │                              │ Publicación         │
│             │                              │ (Hangfire Job)      │
└─────────────┘                              └──────────┬──────────┘
                                                        │
                              ┌─────────────────────────┼────────────────┐
                              │                         │                │
                              ▼                         ▼                ▼
                    ┌─────────────────┐     ┌─────────────────┐  ┌───────────┐
                    │  Parcialmente   │     │   Publicada     │  │  Failed   │
                    │  Publicada      │     │   (Todas)       │  │(Reintentos)│
                    └─────────────────┘     └─────────────────┘  └───────────┘
```

---

## Integración OAuth con Redes Sociales

### Resumen de Integraciones

| Red Social | OAuth Version | PKCE | Duración Access Token | Refresh Token |
|------------|---------------|------|----------------------|---------------|
| Facebook | 2.0 | No | ~60 días (long-lived) | No (usa fb_exchange_token) |
| Instagram | 2.0 (Meta) | No | ~60 días | No |
| X (Twitter) | 2.0 | **Sí (obligatorio)** | 2 horas | Sí (puede rotar) |
| LinkedIn | 2.0 (OpenID) | No | 60 días | Sí (~1 año) |
| TikTok | 2.0 | **Sí (obligatorio)** | 24 horas | Sí (~1 año) |
| YouTube | 2.0 (Google) | No | 1 hora | Sí (no expira) |

### Flujo OAuth 2.0 con PKCE (X/TikTok)

```
┌─────────┐                                           ┌─────────────┐
│ Cliente │                                           │  Proveedor  │
│ (App)   │                                           │   (X, etc)  │
└────┬────┘                                           └──────┬──────┘
     │                                                       │
     │ 1. Generar code_verifier (aleatorio)                  │
     │    Generar code_challenge = SHA256(code_verifier)     │
     │                                                       │
     │ 2. GET /authorize?                                    │
     │    client_id=...&                                     │
     │    code_challenge=...&                                │
     │    code_challenge_method=S256                         │
     │──────────────────────────────────────────────────────►│
     │                                                       │
     │◄────────────────────────────────────────────────────── │
     │ 3. Redirect con ?code=AUTH_CODE                       │
     │                                                       │
     │ 4. POST /token                                        │
     │    code=AUTH_CODE&                                    │
     │    code_verifier=... (el original)                    │
     │──────────────────────────────────────────────────────►│
     │                                                       │
     │◄──────────────────────────────────────────────────────│
     │ 5. { access_token, refresh_token, expires_in }        │
     │                                                       │
```

### Endpoints OAuth Implementados

| Endpoint | Método | Descripción |
|----------|--------|-------------|
| `/oauth/connect/{provider}` | GET | Inicia flujo OAuth, redirige al proveedor |
| `/oauth/callback/{provider}` | GET | Recibe código de autorización, obtiene tokens |
| `/oauth/disconnect/{provider}` | POST | Revoca tokens y elimina conexión |
| `/oauth/reconnect/{provider}` | GET | Reconecta canal que necesita reautorización |

### Scopes por Red Social

```csharp
// Facebook
"pages_manage_posts,pages_read_engagement,pages_show_list,public_profile"

// Instagram
"instagram_business_basic,instagram_business_manage_messages,instagram_business_manage_comments,instagram_business_content_publish"

// X (Twitter)
"tweet.read,tweet.write,users.read,offline.access"

// LinkedIn
"openid,profile,w_member_social"

// TikTok
"user.info.basic,video.publish,video.upload"

// YouTube
"https://www.googleapis.com/auth/youtube.upload,youtube.readonly,userinfo.profile"
```

---

## Configuración de Credenciales

### Estructura de appsettings.json

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost:5432;Database=socialpanelcore;Username=postgres;Password=***"
  },

  "Storage": {
    "UploadsPath": "/var/www/socialpanel/uploads",
    "MaxFileSizeBytes": 10485760,
    "AllowedExtensions": [".jpg", ".jpeg", ".png"]
  },

  "OpenRouter": {
    "ApiKey": "sk-or-v1-...",
    "ApiEndpoint": "https://openrouter.ai/api/v1/chat/completions",
    "ModelId": "kimi-k2-free",
    "Temperature": 0.7,
    "MaxTokens": 500
  },

  "OAuth": {
    "Facebook": {
      "AppId": "YOUR_FB_APP_ID",
      "AppSecret": "YOUR_FB_APP_SECRET",
      "RedirectUri": "/oauth/callback"
    },
    "Instagram": {
      "AppId": "YOUR_IG_APP_ID",
      "AppSecret": "YOUR_IG_APP_SECRET",
      "RedirectUri": "/oauth/callback"
    },
    "TikTok": {
      "ClientKey": "YOUR_TIKTOK_CLIENT_KEY",
      "ClientSecret": "YOUR_TIKTOK_CLIENT_SECRET",
      "RedirectUri": "/oauth/callback"
    },
    "YouTube": {
      "ClientId": "YOUR_GOOGLE_CLIENT_ID",
      "ClientSecret": "YOUR_GOOGLE_CLIENT_SECRET",
      "RedirectUri": "/oauth/callback"
    },
    "LinkedIn": {
      "ClientId": "YOUR_LINKEDIN_CLIENT_ID",
      "ClientSecret": "YOUR_LINKEDIN_CLIENT_SECRET",
      "RedirectUri": "/oauth/callback"
    },
    "X": {
      "ClientId": "YOUR_X_CLIENT_ID",
      "ClientSecret": "YOUR_X_CLIENT_SECRET",
      "RedirectUri": "/oauth/callback/x"
    }
  },

  "X_Legacy": {
    "ConsumerKey": "YOUR_X_CONSUMER_KEY",
    "ConsumerSecret": "YOUR_X_CONSUMER_SECRET",
    "ApiBaseUrl": "https://api.x.com"
  }
}
```

### Guía de Obtención de Credenciales

#### Facebook / Instagram

1. Ir a [developers.facebook.com/apps](https://developers.facebook.com/apps/)
2. Crear App tipo "Business"
3. Agregar productos: "Facebook Login" e "Instagram Graph API"
4. Configurar "Valid OAuth Redirect URIs"
5. Copiar **App ID** y **App Secret** desde Settings > Basic

#### X (Twitter)

1. Ir a [developer.twitter.com](https://developer.twitter.com/en/portal/projects-and-apps)
2. Crear proyecto y app
3. En "User authentication settings", habilitar OAuth 2.0
4. Tipo de App: "Web App"
5. Configurar Callback URI
6. Copiar **Client ID** y **Client Secret**

#### TikTok

1. Ir a [developers.tiktok.com](https://developers.tiktok.com/)
2. Crear App con Login Kit
3. Agregar scopes: `user.info.basic`, `video.publish`, `video.upload`
4. Configurar Redirect URI
5. Copiar **Client Key** y **Client Secret**

#### YouTube (Google)

1. Ir a [console.cloud.google.com](https://console.cloud.google.com/)
2. Crear proyecto
3. Habilitar "YouTube Data API v3"
4. Configurar OAuth consent screen
5. Crear credenciales OAuth 2.0 (Web application)
6. Agregar Authorized redirect URIs
7. Copiar **Client ID** y **Client Secret**

#### LinkedIn

1. Ir a [linkedin.com/developers/apps](https://www.linkedin.com/developers/apps)
2. Crear App y verificar compañía asociada
3. Agregar productos: "Share on LinkedIn", "Sign In with LinkedIn"
4. Configurar OAuth 2.0 Authorized redirect URLs
5. Copiar **Client ID** y **Client Secret** desde Auth tab

### Almacenamiento Seguro de Tokens

Los tokens OAuth se almacenan **cifrados** en la base de datos usando ASP.NET Core Data Protection:

```csharp
// Cifrado al guardar
channel.AccessToken = _protector.Protect(result.AccessToken!);
channel.RefreshToken = _protector.Protect(result.RefreshToken);

// Descifrado al usar
var decryptedToken = _protector.Unprotect(channel.AccessToken);
```

---

## Jobs en Background (Hangfire)

### Jobs Configurados

| Job ID | Servicio | Cron | Descripción |
|--------|----------|------|-------------|
| `adaptar-contenido-ia` | IContentAdaptationService | `0 */3 * * *` (cada 3h) | Adapta posts pendientes usando IA |
| `publicar-posts-programados` | ISocialPublisherService | `*/5 * * * *` (cada 5min) | Publica posts programados |
| `refrescar-tokens-oauth` | TokenRefreshJob | `*/15 * * * *` (cada 15min) | Renueva tokens próximos a expirar |
| `limpiar-estados-oauth` | TokenRefreshJob | `0 * * * *` (cada hora) | Limpia estados OAuth expirados |
| `verificar-salud-canales` | ChannelHealthCheckJob | `0 */2 * * *` (cada 2h) | Verifica conexiones OAuth activas |
| `limpiar-notificaciones-expiradas` | ChannelHealthCheckJob | `0 3 * * *` (3:00 AM) | Elimina notificaciones antiguas |

### Dashboard de Hangfire

Disponible en `/hangfire` (requiere autenticación):

```csharp
app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization = new[] { new HangfireAuthorizationFilter() },
    DashboardTitle = "SocialPanelCore - Trabajos en Background"
});
```

---

## Sistema de Notificaciones

### Tipos de Notificaciones

```csharp
public enum NotificationType
{
    Info = 0,                // Información general
    Warning = 1,             // Advertencia
    Error = 2,               // Error crítico
    Success = 3,             // Operación exitosa
    OAuthReauthRequired = 4, // Requiere reconexión OAuth
    OAuthTokenExpiring = 5,  // Token próximo a expirar
    PublishError = 6,        // Error de publicación
    HealthCheckFailed = 7    // Health check fallido
}
```

### Flujo de Notificaciones OAuth

```
┌─────────────────┐     ┌────────────────────┐     ┌─────────────────┐
│ TokenRefreshJob │     │ Token refresh falla│     │ Crear notificación│
│ detecta token   │────►│ con invalid_grant  │────►│ OAuthReauthRequired│
│ por expirar     │     │                    │     │ para el usuario   │
└─────────────────┘     └────────────────────┘     └─────────┬─────────┘
                                                              │
                                                              ▼
                                                    ┌─────────────────┐
                                                    │ Usuario ve      │
                                                    │ campana en UI   │
                                                    │ con acción      │
                                                    │ "Reconectar"    │
                                                    └─────────────────┘
```

---

## Instalación y Configuración

### Requisitos

- .NET 10.0 SDK
- PostgreSQL 14+
- Node.js (para assets de MudBlazor)

### Pasos de Instalación

```bash
# 1. Clonar repositorio
git clone https://github.com/PopeDev/SocialPanelCore.git
cd SocialPanelCore

# 2. Configurar appsettings.json con credenciales OAuth

# 3. Restaurar dependencias
dotnet restore

# 4. Crear base de datos y aplicar migraciones
dotnet ef database update

# 5. Ejecutar aplicación
dotnet run
```

### Variables de Entorno (Producción)

Para producción, se recomienda usar User Secrets o variables de entorno:

```bash
# Base de datos
export ConnectionStrings__DefaultConnection="Host=..."

# OAuth (ejemplo Facebook)
export OAuth__Facebook__AppId="..."
export OAuth__Facebook__AppSecret="..."

# OpenRouter (IA)
export OpenRouter__ApiKey="sk-or-v1-..."
```

---

## Estructura del Proyecto

```
SocialPanelCore/
├── SocialPanelCore.sln
├── appsettings.json                    # Configuración principal
├── Program.cs                          # Entry point y configuración DI
│
├── Components/                         # Blazor Components
│   ├── App.razor                       # Componente raíz
│   ├── Routes.razor                    # Configuración de rutas
│   ├── Layout/
│   │   ├── MainLayout.razor            # Layout principal
│   │   └── NavMenu.razor               # Menú de navegación
│   ├── Pages/
│   │   ├── Home.razor                  # Dashboard
│   │   ├── Accounts/                   # Gestión de cuentas
│   │   ├── Publications/               # CRUD de publicaciones
│   │   │   ├── Index.razor             # Lista de posts
│   │   │   ├── New.razor               # Crear post
│   │   │   ├── Edit.razor              # Editar post
│   │   │   └── Preview.razor           # Vista previa
│   │   ├── SocialChannels/             # Conexiones de redes
│   │   │   └── Index.razor             # Panel de conexiones
│   │   ├── Users/                      # Gestión de usuarios
│   │   └── Reviews/                    # Aprobación de posts
│   ├── Shared/
│   │   ├── SocialConnectionCard.razor  # Card de conexión OAuth
│   │   ├── NotificationBell.razor      # Campana de notificaciones
│   │   └── Dialogs/                    # Diálogos modales
│   └── Account/                        # Componentes de Identity
│
├── Controllers/
│   ├── OAuthController.cs              # Endpoints OAuth
│   └── DataDeletionController.cs       # GDPR Facebook compliance
│
├── Hangfire/
│   ├── TokenRefreshJob.cs              # Job de renovación de tokens
│   ├── ChannelHealthCheckJob.cs        # Job de health check
│   └── HangfireAuthorizationFilter.cs  # Filtro de autorización dashboard
│
├── Migrations/                         # EF Core migrations
│
├── SocialPanelCore.Domain/             # CAPA DE DOMINIO
│   ├── Entities/
│   │   ├── Account.cs
│   │   ├── User.cs
│   │   ├── BasePost.cs
│   │   ├── AdaptedPost.cs
│   │   ├── PostMedia.cs
│   │   ├── PostTargetNetwork.cs
│   │   ├── SocialChannelConfig.cs
│   │   ├── OAuthState.cs
│   │   ├── Notification.cs
│   │   └── UserAccountAccess.cs
│   ├── Enums/
│   │   ├── NetworkType.cs
│   │   ├── BasePostState.cs
│   │   ├── AdaptedPostState.cs
│   │   ├── ConnectionStatus.cs
│   │   ├── AuthMethod.cs
│   │   ├── UserRole.cs
│   │   ├── PublishMode.cs
│   │   ├── HealthStatus.cs
│   │   └── NotificationType.cs
│   ├── Interfaces/
│   │   ├── IAccountService.cs
│   │   ├── IOAuthService.cs
│   │   ├── ISocialPublisherService.cs
│   │   ├── IContentAdaptationService.cs
│   │   ├── IAiContentService.cs
│   │   ├── ITokenRefreshService.cs
│   │   ├── INotificationService.cs
│   │   └── ...
│   └── Configuration/
│       └── StorageSettings.cs
│
├── SocialPanelCore.Infrastructure/     # CAPA DE INFRAESTRUCTURA
│   ├── Data/
│   │   └── ApplicationDbContext.cs     # DbContext EF Core
│   ├── Services/
│   │   ├── AccountService.cs
│   │   ├── OAuthService.cs             # Flujos OAuth (FB, IG, X, etc.)
│   │   ├── OAuthStateStore.cs          # Almacén de estados PKCE
│   │   ├── TokenRefreshService.cs
│   │   ├── SocialPublisherService.cs   # Publicación en redes
│   │   ├── ContentAdaptationService.cs
│   │   ├── AiContentService.cs         # Integración OpenRouter
│   │   ├── MediaStorageService.cs
│   │   ├── NotificationService.cs
│   │   └── UserService.cs
│   ├── ExternalApis/
│   │   ├── X/IXApiClient.cs            # Refit client para X API
│   │   ├── Meta/IMetaGraphApiClient.cs # Refit client para Meta
│   │   ├── TikTok/ITikTokApiClient.cs  # Refit client para TikTok
│   │   └── YouTube/YouTubeApiService.cs# Google SDK para YouTube
│   └── Helpers/
│       └── OAuth1Helper.cs             # Helper para OAuth 1.0a (X)
│
└── wwwroot/                            # Assets estáticos
```

---

## Licencia

Este proyecto es privado y propietario de PopeDev.

---

## Contacto

Para soporte técnico o consultas sobre el proyecto, contactar al equipo de desarrollo.
