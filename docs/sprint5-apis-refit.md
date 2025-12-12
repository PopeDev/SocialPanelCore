# Sprint 5: Integración con APIs Externas (Refit)

**Duración estimada:** 7-10 días
**Prerrequisitos:** Sprints 1-4 completados

---

## Objetivo del Sprint

Implementar la integración real con las APIs de redes sociales usando:
- **Refit** para X, Instagram, Facebook y TikTok
- **SDK oficial de Google** para YouTube

---

## Tecnologías a Usar

| Red | Librería | Motivo |
|-----|----------|--------|
| YouTube | `Google.Apis.YouTube.v3` | SDK oficial, estable y bien documentado |
| X (Twitter) | Refit | No hay SDK oficial .NET moderno |
| Instagram | Refit (Meta Graph API) | Meta no ofrece SDK .NET oficial |
| Facebook | Refit (Graph API) | Meta no ofrece SDK .NET oficial |
| TikTok | Refit | No hay SDK .NET oficial |

---

## Tareas

### Tarea 5.1: Instalar Paquetes NuGet

**Ejecutar en la terminal:**

```bash
cd /home/user/SocialPanelCore

# Refit
dotnet add package Refit --version 7.1.2
dotnet add package Refit.HttpClientFactory --version 7.1.2

# Google YouTube SDK
dotnet add package Google.Apis.YouTube.v3 --version 1.68.0.3520
dotnet add package Google.Apis.Auth --version 1.68.0
```

---

### Tarea 5.2: Crear Interfaces Refit para X (Twitter)

**Archivo a crear:** `SocialPanelCore.Infrastructure/ExternalApis/X/IXApiClient.cs`

```csharp
using Refit;

namespace SocialPanelCore.Infrastructure.ExternalApis.X;

/// <summary>
/// Cliente Refit para X (Twitter) API v2
/// Base URL: https://api.x.com
/// Docs: https://developer.x.com/en/docs/twitter-api
/// </summary>
public interface IXApiClient
{
    /// <summary>
    /// Crear un nuevo tweet
    /// POST /2/tweets
    /// </summary>
    [Post("/2/tweets")]
    Task<XTweetResponse> CreateTweetAsync(
        [Body] XTweetRequest request,
        [Header("Authorization")] string authorization);

    /// <summary>
    /// Eliminar un tweet
    /// DELETE /2/tweets/{id}
    /// </summary>
    [Delete("/2/tweets/{id}")]
    Task<XDeleteResponse> DeleteTweetAsync(
        string id,
        [Header("Authorization")] string authorization);

    /// <summary>
    /// Obtener información de un tweet
    /// GET /2/tweets/{id}
    /// </summary>
    [Get("/2/tweets/{id}")]
    Task<XTweetLookupResponse> GetTweetAsync(
        string id,
        [Header("Authorization")] string authorization,
        [Query] string? expansions = null,
        [Query("tweet.fields")] string? tweetFields = null);

    /// <summary>
    /// Obtener usuario autenticado
    /// GET /2/users/me
    /// </summary>
    [Get("/2/users/me")]
    Task<XUserResponse> GetCurrentUserAsync(
        [Header("Authorization")] string authorization);

    /// <summary>
    /// Subir media (imagen) - Step 1: INIT
    /// POST /2/media/upload (multipart)
    /// </summary>
    [Multipart]
    [Post("/2/media/upload")]
    Task<XMediaUploadInitResponse> InitMediaUploadAsync(
        [AliasAs("command")] string command,
        [AliasAs("total_bytes")] long totalBytes,
        [AliasAs("media_type")] string mediaType,
        [Header("Authorization")] string authorization);
}

#region Request/Response Models

public class XTweetRequest
{
    public string Text { get; set; } = string.Empty;
    public XTweetMedia? Media { get; set; }
}

public class XTweetMedia
{
    public List<string>? MediaIds { get; set; }
}

public class XTweetResponse
{
    public XTweetData? Data { get; set; }
}

public class XTweetData
{
    public string? Id { get; set; }
    public string? Text { get; set; }
}

public class XDeleteResponse
{
    public XDeleteData? Data { get; set; }
}

public class XDeleteData
{
    public bool Deleted { get; set; }
}

public class XTweetLookupResponse
{
    public XTweetData? Data { get; set; }
}

public class XUserResponse
{
    public XUserData? Data { get; set; }
}

public class XUserData
{
    public string? Id { get; set; }
    public string? Name { get; set; }
    public string? Username { get; set; }
}

public class XMediaUploadInitResponse
{
    public string? MediaIdString { get; set; }
    public long? ExpiresAfterSecs { get; set; }
}

#endregion
```

---

### Tarea 5.3: Crear Interfaces Refit para Meta (Facebook/Instagram)

**Archivo a crear:** `SocialPanelCore.Infrastructure/ExternalApis/Meta/IMetaGraphApiClient.cs`

```csharp
using Refit;

namespace SocialPanelCore.Infrastructure.ExternalApis.Meta;

/// <summary>
/// Cliente Refit para Meta Graph API (Facebook e Instagram)
/// Base URL: https://graph.facebook.com/v18.0
/// </summary>
public interface IMetaGraphApiClient
{
    #region Facebook

    /// <summary>
    /// Obtener páginas del usuario
    /// GET /me/accounts
    /// </summary>
    [Get("/me/accounts")]
    Task<MetaPagesResponse> GetUserPagesAsync(
        [Query("access_token")] string accessToken);

    /// <summary>
    /// Publicar en el feed de una página
    /// POST /{page-id}/feed
    /// </summary>
    [Post("/{pageId}/feed")]
    Task<MetaPostResponse> CreateFacebookPostAsync(
        string pageId,
        [Query("message")] string message,
        [Query("access_token")] string accessToken);

    /// <summary>
    /// Publicar con imagen en página
    /// POST /{page-id}/photos
    /// </summary>
    [Multipart]
    [Post("/{pageId}/photos")]
    Task<MetaPostResponse> CreateFacebookPhotoPostAsync(
        string pageId,
        [AliasAs("message")] string message,
        [AliasAs("url")] string imageUrl,
        [Query("access_token")] string accessToken);

    /// <summary>
    /// Eliminar un post de página
    /// DELETE /{post-id}
    /// </summary>
    [Delete("/{postId}")]
    Task<MetaDeleteResponse> DeleteFacebookPostAsync(
        string postId,
        [Query("access_token")] string accessToken);

    #endregion

    #region Instagram

    /// <summary>
    /// Obtener cuenta de Instagram Business vinculada a página
    /// GET /{page-id}?fields=instagram_business_account
    /// </summary>
    [Get("/{pageId}")]
    Task<MetaInstagramAccountResponse> GetInstagramAccountAsync(
        string pageId,
        [Query("fields")] string fields,
        [Query("access_token")] string accessToken);

    /// <summary>
    /// Crear contenedor de media para Instagram (Step 1)
    /// POST /{ig-user-id}/media
    /// Para imagen: image_url + caption
    /// Para video/reel: video_url + caption + media_type=REELS
    /// </summary>
    [Post("/{igUserId}/media")]
    Task<MetaMediaContainerResponse> CreateInstagramMediaContainerAsync(
        string igUserId,
        [Query("image_url")] string? imageUrl,
        [Query("video_url")] string? videoUrl,
        [Query("caption")] string caption,
        [Query("media_type")] string? mediaType,
        [Query("access_token")] string accessToken);

    /// <summary>
    /// Verificar estado del contenedor (Step 2 - opcional para videos)
    /// GET /{container-id}?fields=status_code
    /// </summary>
    [Get("/{containerId}")]
    Task<MetaContainerStatusResponse> GetContainerStatusAsync(
        string containerId,
        [Query("fields")] string fields,
        [Query("access_token")] string accessToken);

    /// <summary>
    /// Publicar media de Instagram (Step 3)
    /// POST /{ig-user-id}/media_publish
    /// </summary>
    [Post("/{igUserId}/media_publish")]
    Task<MetaPostResponse> PublishInstagramMediaAsync(
        string igUserId,
        [Query("creation_id")] string containerId,
        [Query("access_token")] string accessToken);

    /// <summary>
    /// Obtener media del perfil de Instagram
    /// GET /{ig-user-id}/media
    /// </summary>
    [Get("/{igUserId}/media")]
    Task<MetaMediaListResponse> GetInstagramMediaAsync(
        string igUserId,
        [Query("access_token")] string accessToken);

    #endregion
}

#region Response Models

public class MetaPagesResponse
{
    public List<MetaPage>? Data { get; set; }
}

public class MetaPage
{
    public string? Id { get; set; }
    public string? Name { get; set; }
    public string? AccessToken { get; set; }
}

public class MetaPostResponse
{
    public string? Id { get; set; }
}

public class MetaDeleteResponse
{
    public bool Success { get; set; }
}

public class MetaInstagramAccountResponse
{
    public MetaInstagramBusinessAccount? InstagramBusinessAccount { get; set; }
}

public class MetaInstagramBusinessAccount
{
    public string? Id { get; set; }
}

public class MetaMediaContainerResponse
{
    public string? Id { get; set; }
}

public class MetaContainerStatusResponse
{
    public string? StatusCode { get; set; }  // IN_PROGRESS, FINISHED, ERROR
}

public class MetaMediaListResponse
{
    public List<MetaMediaItem>? Data { get; set; }
}

public class MetaMediaItem
{
    public string? Id { get; set; }
    public string? MediaType { get; set; }
    public string? Caption { get; set; }
    public string? Permalink { get; set; }
}

#endregion
```

---

### Tarea 5.4: Crear Interfaces Refit para TikTok

**Archivo a crear:** `SocialPanelCore.Infrastructure/ExternalApis/TikTok/ITikTokApiClient.cs`

```csharp
using Refit;

namespace SocialPanelCore.Infrastructure.ExternalApis.TikTok;

/// <summary>
/// Cliente Refit para TikTok Content Posting API
/// Base URL: https://open.tiktokapis.com
/// Docs: https://developers.tiktok.com/doc/content-posting-api-get-started
/// </summary>
public interface ITikTokApiClient
{
    /// <summary>
    /// Inicializar publicación de foto
    /// POST /v2/post/publish/content/init/
    /// </summary>
    [Post("/v2/post/publish/content/init/")]
    Task<TikTokInitResponse> InitPhotoPublishAsync(
        [Body] TikTokPhotoPublishRequest request,
        [Header("Authorization")] string authorization,
        [Header("Content-Type")] string contentType = "application/json; charset=UTF-8");

    /// <summary>
    /// Inicializar publicación de video (modo inbox/draft)
    /// POST /v2/post/publish/inbox/video/init/
    /// </summary>
    [Post("/v2/post/publish/inbox/video/init/")]
    Task<TikTokVideoInitResponse> InitVideoPublishAsync(
        [Body] TikTokVideoPublishRequest request,
        [Header("Authorization")] string authorization,
        [Header("Content-Type")] string contentType = "application/json; charset=UTF-8");

    /// <summary>
    /// Consultar estado de publicación
    /// POST /v2/post/publish/status/fetch/
    /// </summary>
    [Post("/v2/post/publish/status/fetch/")]
    Task<TikTokStatusResponse> GetPublishStatusAsync(
        [Body] TikTokStatusRequest request,
        [Header("Authorization")] string authorization,
        [Header("Content-Type")] string contentType = "application/json; charset=UTF-8");

    /// <summary>
    /// Obtener información del usuario
    /// GET /v2/user/info/
    /// </summary>
    [Get("/v2/user/info/")]
    Task<TikTokUserInfoResponse> GetUserInfoAsync(
        [Header("Authorization")] string authorization,
        [Query("fields")] string fields = "open_id,union_id,avatar_url,display_name");
}

#region Request/Response Models

public class TikTokPhotoPublishRequest
{
    public TikTokPostInfo PostInfo { get; set; } = new();
    public TikTokSourceInfo SourceInfo { get; set; } = new();
}

public class TikTokVideoPublishRequest
{
    public TikTokSourceInfo SourceInfo { get; set; } = new();
}

public class TikTokPostInfo
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool DisableDuet { get; set; } = false;
    public bool DisableComment { get; set; } = false;
    public bool DisableStitch { get; set; } = false;
    public string PrivacyLevel { get; set; } = "PUBLIC_TO_EVERYONE";  // o MUTUAL_FOLLOW_FRIENDS, SELF_ONLY
}

public class TikTokSourceInfo
{
    public string Source { get; set; } = "PULL_FROM_URL";  // o FILE_UPLOAD
    public string? PhotoCoverIndex { get; set; }
    public List<string>? PhotoImages { get; set; }  // URLs de imágenes
    public long? VideoSize { get; set; }  // Para videos
    public int? ChunkSize { get; set; }   // Para videos grandes
    public int? TotalChunkCount { get; set; }
}

public class TikTokInitResponse
{
    public TikTokInitData? Data { get; set; }
    public TikTokError? Error { get; set; }
}

public class TikTokInitData
{
    public string? PublishId { get; set; }
}

public class TikTokVideoInitResponse
{
    public TikTokVideoInitData? Data { get; set; }
    public TikTokError? Error { get; set; }
}

public class TikTokVideoInitData
{
    public string? PublishId { get; set; }
    public string? UploadUrl { get; set; }
}

public class TikTokStatusRequest
{
    public string PublishId { get; set; } = string.Empty;
}

public class TikTokStatusResponse
{
    public TikTokStatusData? Data { get; set; }
    public TikTokError? Error { get; set; }
}

public class TikTokStatusData
{
    public string? Status { get; set; }  // PROCESSING_UPLOAD, PROCESSING_DOWNLOAD, SEND_TO_USER_INBOX, PUBLISH_COMPLETE, FAILED
    public string? PubliclyAvailablePostId { get; set; }
    public List<string>? FailReason { get; set; }
}

public class TikTokUserInfoResponse
{
    public TikTokUserData? Data { get; set; }
    public TikTokError? Error { get; set; }
}

public class TikTokUserData
{
    public TikTokUser? User { get; set; }
}

public class TikTokUser
{
    public string? OpenId { get; set; }
    public string? UnionId { get; set; }
    public string? AvatarUrl { get; set; }
    public string? DisplayName { get; set; }
}

public class TikTokError
{
    public string? Code { get; set; }
    public string? Message { get; set; }
    public string? LogId { get; set; }
}

#endregion
```

---

### Tarea 5.5: Configurar Clientes Refit en Program.cs

**Archivo a modificar:** `Program.cs`

Añadir después de los servicios existentes:

```csharp
using Refit;
using SocialPanelCore.Infrastructure.ExternalApis.X;
using SocialPanelCore.Infrastructure.ExternalApis.Meta;
using SocialPanelCore.Infrastructure.ExternalApis.TikTok;

// ... otros servicios ...

// Configurar clientes Refit
builder.Services.AddRefitClient<IXApiClient>()
    .ConfigureHttpClient(c => c.BaseAddress = new Uri("https://api.x.com"));

builder.Services.AddRefitClient<IMetaGraphApiClient>()
    .ConfigureHttpClient(c => c.BaseAddress = new Uri("https://graph.facebook.com/v18.0"));

builder.Services.AddRefitClient<ITikTokApiClient>()
    .ConfigureHttpClient(c => c.BaseAddress = new Uri("https://open.tiktokapis.com"));
```

---

### Tarea 5.6: Configurar YouTube SDK

**Archivo a crear:** `SocialPanelCore.Infrastructure/ExternalApis/YouTube/YouTubeService.cs`

```csharp
using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.YouTube.v3;
using Google.Apis.YouTube.v3.Data;
using Microsoft.Extensions.Logging;

namespace SocialPanelCore.Infrastructure.ExternalApis.YouTube;

/// <summary>
/// Servicio para interactuar con YouTube Data API v3
/// </summary>
public class YouTubeApiService
{
    private readonly ILogger<YouTubeApiService> _logger;

    public YouTubeApiService(ILogger<YouTubeApiService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Crea un cliente de YouTube autenticado
    /// </summary>
    private YouTubeService CreateClient(string accessToken)
    {
        var credential = GoogleCredential.FromAccessToken(accessToken);

        return new YouTubeService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
            ApplicationName = "SocialPanelCore"
        });
    }

    /// <summary>
    /// Sube un video a YouTube
    /// </summary>
    public async Task<string> UploadVideoAsync(
        string accessToken,
        Stream videoStream,
        string title,
        string description,
        string[] tags,
        string privacyStatus = "public")
    {
        var youtubeService = CreateClient(accessToken);

        var video = new Video
        {
            Snippet = new VideoSnippet
            {
                Title = title,
                Description = description,
                Tags = tags,
                CategoryId = "22"  // People & Blogs
            },
            Status = new VideoStatus
            {
                PrivacyStatus = privacyStatus  // "public", "private", "unlisted"
            }
        };

        var videosInsertRequest = youtubeService.Videos.Insert(
            video,
            "snippet,status",
            videoStream,
            "video/*");

        videosInsertRequest.ProgressChanged += progress =>
        {
            _logger.LogDebug("Upload progress: {Status} - {BytesSent} bytes",
                progress.Status, progress.BytesSent);
        };

        videosInsertRequest.ResponseReceived += video =>
        {
            _logger.LogInformation("Video subido: {VideoId}", video.Id);
        };

        var uploadProgress = await videosInsertRequest.UploadAsync();

        if (uploadProgress.Status == Google.Apis.Upload.UploadStatus.Failed)
        {
            throw new Exception($"Error subiendo video: {uploadProgress.Exception?.Message}");
        }

        return videosInsertRequest.ResponseBody?.Id
            ?? throw new Exception("No se recibió ID del video");
    }

    /// <summary>
    /// Obtiene información de un video
    /// </summary>
    public async Task<Video?> GetVideoAsync(string accessToken, string videoId)
    {
        var youtubeService = CreateClient(accessToken);

        var request = youtubeService.Videos.List("snippet,status,statistics");
        request.Id = videoId;

        var response = await request.ExecuteAsync();
        return response.Items?.FirstOrDefault();
    }

    /// <summary>
    /// Elimina un video
    /// </summary>
    public async Task DeleteVideoAsync(string accessToken, string videoId)
    {
        var youtubeService = CreateClient(accessToken);
        await youtubeService.Videos.Delete(videoId).ExecuteAsync();
        _logger.LogInformation("Video eliminado: {VideoId}", videoId);
    }

    /// <summary>
    /// Actualiza información de un video
    /// </summary>
    public async Task<Video> UpdateVideoAsync(
        string accessToken,
        string videoId,
        string title,
        string description)
    {
        var youtubeService = CreateClient(accessToken);

        // Primero obtener el video actual
        var listRequest = youtubeService.Videos.List("snippet");
        listRequest.Id = videoId;
        var listResponse = await listRequest.ExecuteAsync();
        var video = listResponse.Items?.FirstOrDefault()
            ?? throw new Exception($"Video no encontrado: {videoId}");

        // Actualizar campos
        video.Snippet.Title = title;
        video.Snippet.Description = description;

        var updateRequest = youtubeService.Videos.Update(video, "snippet");
        return await updateRequest.ExecuteAsync();
    }

    /// <summary>
    /// Obtiene información del canal del usuario
    /// </summary>
    public async Task<Channel?> GetMyChannelAsync(string accessToken)
    {
        var youtubeService = CreateClient(accessToken);

        var request = youtubeService.Channels.List("snippet,statistics");
        request.Mine = true;

        var response = await request.ExecuteAsync();
        return response.Items?.FirstOrDefault();
    }
}
```

---

### Tarea 5.7: Refactorizar SocialPublisherService para Usar Refit

**Archivo a modificar:** `SocialPanelCore.Infrastructure/Services/SocialPublisherService.cs`

Refactorizar para inyectar los clientes Refit:

```csharp
using SocialPanelCore.Infrastructure.ExternalApis.X;
using SocialPanelCore.Infrastructure.ExternalApis.Meta;
using SocialPanelCore.Infrastructure.ExternalApis.TikTok;
using SocialPanelCore.Infrastructure.ExternalApis.YouTube;

public class SocialPublisherService : ISocialPublisherService
{
    private readonly ApplicationDbContext _context;
    private readonly ISocialChannelConfigService _channelConfigService;
    private readonly IXApiClient _xApiClient;
    private readonly IMetaGraphApiClient _metaApiClient;
    private readonly ITikTokApiClient _tikTokApiClient;
    private readonly YouTubeApiService _youTubeService;
    private readonly ILogger<SocialPublisherService> _logger;

    public SocialPublisherService(
        ApplicationDbContext context,
        ISocialChannelConfigService channelConfigService,
        IXApiClient xApiClient,
        IMetaGraphApiClient metaApiClient,
        ITikTokApiClient tikTokApiClient,
        YouTubeApiService youTubeService,
        ILogger<SocialPublisherService> logger)
    {
        _context = context;
        _channelConfigService = channelConfigService;
        _xApiClient = xApiClient;
        _metaApiClient = metaApiClient;
        _tikTokApiClient = tikTokApiClient;
        _youTubeService = youTubeService;
        _logger = logger;
    }

    // ... resto del código ...

    private async Task<string> PublishToXAsync(AdaptedPost post, string authHeader)
    {
        _logger.LogInformation("Publicando en X (Twitter) con Refit...");

        var request = new XTweetRequest
        {
            Text = post.AdaptedContent
        };

        var response = await _xApiClient.CreateTweetAsync(request, authHeader);

        return response.Data?.Id
            ?? throw new Exception("No se recibió ID del tweet");
    }

    private async Task<string> PublishToFacebookAsync(
        AdaptedPost post,
        string accessToken,
        SocialChannelConfig config)
    {
        _logger.LogInformation("Publicando en Facebook con Refit...");

        // Obtener páginas
        var pagesResponse = await _metaApiClient.GetUserPagesAsync(accessToken);
        var page = pagesResponse.Data?.FirstOrDefault()
            ?? throw new Exception("No se encontraron páginas de Facebook");

        // Publicar usando el token de la página
        var pageToken = page.AccessToken ?? accessToken;
        var response = await _metaApiClient.CreateFacebookPostAsync(
            page.Id!,
            post.AdaptedContent,
            pageToken);

        return response.Id
            ?? throw new Exception("No se recibió ID del post de Facebook");
    }

    private async Task<string> PublishToInstagramAsync(
        AdaptedPost post,
        string accessToken,
        SocialChannelConfig config,
        string? imageUrl = null)
    {
        _logger.LogInformation("Publicando en Instagram con Refit...");

        // Obtener páginas y cuenta de Instagram
        var pagesResponse = await _metaApiClient.GetUserPagesAsync(accessToken);
        var page = pagesResponse.Data?.FirstOrDefault()
            ?? throw new Exception("No se encontraron páginas de Facebook");

        var igAccountResponse = await _metaApiClient.GetInstagramAccountAsync(
            page.Id!,
            "instagram_business_account",
            accessToken);

        var igAccountId = igAccountResponse.InstagramBusinessAccount?.Id
            ?? throw new Exception("No se encontró cuenta de Instagram Business");

        if (string.IsNullOrEmpty(imageUrl))
        {
            // Instagram requiere media
            _logger.LogWarning("Instagram requiere imagen. Publicación de solo texto no soportada.");
            return $"ig_no_media_{Guid.NewGuid():N}";
        }

        // Step 1: Crear contenedor
        var containerResponse = await _metaApiClient.CreateInstagramMediaContainerAsync(
            igAccountId,
            imageUrl,
            null,  // videoUrl
            post.AdaptedContent,
            null,  // mediaType
            accessToken);

        var containerId = containerResponse.Id
            ?? throw new Exception("No se pudo crear contenedor de Instagram");

        // Step 2: Verificar estado (para videos, esperar FINISHED)
        // Para imágenes suele ser instantáneo
        var statusResponse = await _metaApiClient.GetContainerStatusAsync(
            containerId,
            "status_code",
            accessToken);

        if (statusResponse.StatusCode == "ERROR")
        {
            throw new Exception("Error procesando media de Instagram");
        }

        // Step 3: Publicar
        var publishResponse = await _metaApiClient.PublishInstagramMediaAsync(
            igAccountId,
            containerId,
            accessToken);

        return publishResponse.Id
            ?? throw new Exception("No se recibió ID del post de Instagram");
    }

    private async Task<string> PublishToTikTokAsync(
        AdaptedPost post,
        string accessToken,
        List<string>? imageUrls = null)
    {
        _logger.LogInformation("Publicando en TikTok con Refit...");

        if (imageUrls == null || !imageUrls.Any())
        {
            _logger.LogWarning("TikTok requiere contenido multimedia");
            return $"tt_no_media_{Guid.NewGuid():N}";
        }

        var request = new TikTokPhotoPublishRequest
        {
            PostInfo = new TikTokPostInfo
            {
                Title = post.BasePost?.Title ?? "",
                Description = post.AdaptedContent,
                PrivacyLevel = "PUBLIC_TO_EVERYONE"
            },
            SourceInfo = new TikTokSourceInfo
            {
                Source = "PULL_FROM_URL",
                PhotoImages = imageUrls,
                PhotoCoverIndex = "0"
            }
        };

        var authHeader = $"Bearer {accessToken}";
        var response = await _tikTokApiClient.InitPhotoPublishAsync(request, authHeader);

        if (response.Error != null)
        {
            throw new Exception($"Error de TikTok: {response.Error.Message}");
        }

        var publishId = response.Data?.PublishId
            ?? throw new Exception("No se recibió PublishId de TikTok");

        // Polling para verificar estado
        for (int i = 0; i < 10; i++)
        {
            await Task.Delay(3000); // Esperar 3 segundos

            var statusResponse = await _tikTokApiClient.GetPublishStatusAsync(
                new TikTokStatusRequest { PublishId = publishId },
                authHeader);

            if (statusResponse.Data?.Status == "PUBLISH_COMPLETE")
            {
                return statusResponse.Data.PubliclyAvailablePostId ?? publishId;
            }

            if (statusResponse.Data?.Status == "FAILED")
            {
                var reasons = string.Join(", ", statusResponse.Data.FailReason ?? new List<string>());
                throw new Exception($"Publicación de TikTok falló: {reasons}");
            }
        }

        // Si después de 30 segundos sigue procesando, devolver el publishId
        _logger.LogWarning("TikTok sigue procesando después de 30s, devolviendo publishId");
        return publishId;
    }
}
```

---

### Tarea 5.8: Registrar YouTubeApiService

**Archivo a modificar:** `Program.cs`

```csharp
// Registrar servicio de YouTube
builder.Services.AddScoped<YouTubeApiService>();
```

---

### Tarea 5.9: Crear Helper para OAuth Headers (X/Twitter)

**Archivo a crear:** `SocialPanelCore.Infrastructure/Helpers/OAuth1Helper.cs`

```csharp
using System.Security.Cryptography;
using System.Text;

namespace SocialPanelCore.Infrastructure.Helpers;

/// <summary>
/// Helper para generar headers OAuth 1.0a (requerido por X/Twitter)
/// </summary>
public static class OAuth1Helper
{
    /// <summary>
    /// Genera el header Authorization para OAuth 1.0a
    /// </summary>
    public static string GenerateOAuth1Header(
        string method,
        string url,
        string consumerKey,
        string consumerSecret,
        string accessToken,
        string accessTokenSecret,
        Dictionary<string, string>? additionalParams = null)
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        var nonce = Convert.ToBase64String(Encoding.UTF8.GetBytes(Guid.NewGuid().ToString("N")));

        var oauthParams = new SortedDictionary<string, string>
        {
            ["oauth_consumer_key"] = consumerKey,
            ["oauth_nonce"] = nonce,
            ["oauth_signature_method"] = "HMAC-SHA1",
            ["oauth_timestamp"] = timestamp,
            ["oauth_token"] = accessToken,
            ["oauth_version"] = "1.0"
        };

        // Añadir parámetros adicionales si los hay
        if (additionalParams != null)
        {
            foreach (var param in additionalParams)
            {
                oauthParams[param.Key] = param.Value;
            }
        }

        // Crear base string
        var paramString = string.Join("&",
            oauthParams.Select(kvp =>
                $"{Uri.EscapeDataString(kvp.Key)}={Uri.EscapeDataString(kvp.Value)}"));

        var baseString = $"{method.ToUpper()}&{Uri.EscapeDataString(url)}&{Uri.EscapeDataString(paramString)}";

        // Crear signing key
        var signingKey = $"{Uri.EscapeDataString(consumerSecret)}&{Uri.EscapeDataString(accessTokenSecret)}";

        // Generar firma
        using var hmac = new HMACSHA1(Encoding.UTF8.GetBytes(signingKey));
        var signatureBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(baseString));
        var signature = Convert.ToBase64String(signatureBytes);

        oauthParams["oauth_signature"] = signature;

        // Crear header (solo parámetros oauth_*)
        var headerValue = string.Join(", ",
            oauthParams
                .Where(kvp => kvp.Key.StartsWith("oauth_"))
                .Select(kvp =>
                    $"{Uri.EscapeDataString(kvp.Key)}=\"{Uri.EscapeDataString(kvp.Value)}\""));

        return $"OAuth {headerValue}";
    }
}
```

---

### Tarea 5.10: Actualizar Configuración de APIs

**Archivo a modificar:** `appsettings.json`

Verificar que existen las configuraciones necesarias:

```json
{
  "OAuth": {
    "Facebook": {
      "AppId": "tu-app-id",
      "AppSecret": "tu-app-secret",
      "RedirectUri": "https://tu-dominio.com/oauth/callback/facebook"
    },
    "Instagram": {
      "AppId": "tu-app-id",
      "AppSecret": "tu-app-secret",
      "RedirectUri": "https://tu-dominio.com/oauth/callback/instagram"
    },
    "TikTok": {
      "ClientKey": "tu-client-key",
      "ClientSecret": "tu-client-secret",
      "RedirectUri": "https://tu-dominio.com/oauth/callback/tiktok"
    },
    "YouTube": {
      "ClientId": "tu-client-id",
      "ClientSecret": "tu-client-secret",
      "RedirectUri": "https://tu-dominio.com/oauth/callback/youtube"
    }
  },
  "X": {
    "ConsumerKey": "tu-consumer-key",
    "ConsumerSecret": "tu-consumer-secret"
  }
}
```

---

## Criterios de Aceptación

- [ ] Se puede publicar en X/Twitter usando Refit
- [ ] Se puede publicar en Facebook usando Refit
- [ ] Se puede publicar en Instagram (con imagen) usando Refit
- [ ] Se puede publicar en TikTok (con imagen) usando Refit
- [ ] Se puede subir video a YouTube usando el SDK oficial
- [ ] Los errores de API se manejan correctamente
- [ ] Los tokens expirados se refrescan automáticamente

---

## Pruebas Manuales

### X (Twitter)
1. Configurar credenciales de X en una cuenta
2. Crear publicación para X
3. Verificar que el tweet aparece en X
4. Verificar que el ExternalPostId es correcto

### Facebook
1. Configurar OAuth de Facebook
2. Crear publicación para página de Facebook
3. Verificar que el post aparece en la página

### Instagram
1. Vincular cuenta de Instagram Business
2. Crear publicación con imagen para Instagram
3. Verificar que el post aparece en Instagram

### TikTok
1. Configurar OAuth de TikTok
2. Crear publicación con imágenes para TikTok
3. Verificar que el post aparece (puede tardar)

### YouTube
1. Configurar OAuth de Google/YouTube
2. Subir video de prueba
3. Verificar que aparece en el canal

---

## Troubleshooting

### Error "Unauthorized" en X

**Causa:** Firma OAuth 1.0a incorrecta o tokens inválidos

**Solución:**
1. Verificar que Consumer Key/Secret son correctos
2. Verificar que Access Token/Secret son correctos
3. Revisar que el timestamp está en UTC

### Error "Invalid token" en Meta (Facebook/Instagram)

**Causa:** Token expirado o permisos insuficientes

**Solución:**
1. Regenerar token de acceso
2. Verificar que la app tiene permisos: `pages_manage_posts`, `instagram_content_publish`

### Error "quota exceeded" en YouTube

**Causa:** Límite de API alcanzado

**Solución:**
1. Verificar cuota en Google Cloud Console
2. Implementar rate limiting
3. Solicitar aumento de cuota si es necesario

---

## Notas Adicionales

### Sobre Media Upload

Para X, Instagram y TikTok, el flujo de subida de medios es complejo:

1. **X:** Requiere endpoint separado `/2/media/upload` con chunks para videos grandes
2. **Instagram:** Requiere que las imágenes estén accesibles por URL pública
3. **TikTok:** Requiere URLs públicas o upload chunked para videos

**Recomendación:** Implementar un servicio de hosting de medios temporal (ej: Azure Blob con SAS tokens) para publicar imágenes que requieren URL pública.

### Sobre Rate Limiting

Cada API tiene límites diferentes:
- **X:** 300 tweets/3 horas (app-level)
- **Meta:** 200 calls/hour (app-level)
- **TikTok:** Varía según endpoint
- **YouTube:** 10,000 units/día

**Recomendación:** Implementar un sistema de cola con rate limiting para publicaciones en masa.

---

## Conclusión

Con este sprint completado, el sistema tendrá integración completa con todas las redes sociales principales usando las mejores prácticas:
- Refit para APIs REST
- SDK oficial para YouTube
- Manejo correcto de OAuth 1.0a y OAuth 2.0
- Gestión de errores y reintentos
