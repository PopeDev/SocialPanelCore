using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SocialPanelCore.Domain.Configuration;
using SocialPanelCore.Domain.Entities;
using SocialPanelCore.Domain.Enums;
using SocialPanelCore.Domain.Interfaces;
using SocialPanelCore.Infrastructure.Data;
using SocialPanelCore.Infrastructure.ExternalApis.Meta;
using SocialPanelCore.Infrastructure.ExternalApis.TikTok;
using SocialPanelCore.Infrastructure.ExternalApis.X;
using SocialPanelCore.Infrastructure.ExternalApis.YouTube;
using SocialPanelCore.Infrastructure.Helpers;

namespace SocialPanelCore.Infrastructure.Services;

public class SocialPublisherService : ISocialPublisherService
{
    private readonly ApplicationDbContext _context;
    private readonly ISocialChannelConfigService _channelConfigService;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IXApiClient _xApiClient;
    private readonly IMetaGraphApiClient _metaApiClient;
    private readonly ITikTokApiClient _tikTokApiClient;
    private readonly YouTubeApiService _youTubeService;
    private readonly StorageSettings _storageSettings;
    private readonly ILogger<SocialPublisherService> _logger;

    public SocialPublisherService(
        ApplicationDbContext context,
        ISocialChannelConfigService channelConfigService,
        IHttpClientFactory httpClientFactory,
        IXApiClient xApiClient,
        IMetaGraphApiClient metaApiClient,
        ITikTokApiClient tikTokApiClient,
        YouTubeApiService youTubeService,
        IOptions<StorageSettings> storageSettings,
        ILogger<SocialPublisherService> logger)
    {
        _context = context;
        _channelConfigService = channelConfigService;
        _httpClientFactory = httpClientFactory;
        _xApiClient = xApiClient;
        _metaApiClient = metaApiClient;
        _tikTokApiClient = tikTokApiClient;
        _youTubeService = youTubeService;
        _storageSettings = storageSettings.Value;
        _logger = logger;
    }

    public async Task PublishScheduledPostsAsync()
    {
        _logger.LogInformation("Iniciando publicacion de posts programados");

        var now = DateTime.UtcNow;

        // Obtener posts adaptados listos para publicar
        var postsToPublish = await _context.BasePosts
            .Include(p => p.AdaptedVersions)
            .Include(p => p.Account)
                .ThenInclude(a => a.SocialChannels)
            .Where(p => p.State == BasePostState.Adaptada &&
                        p.ScheduledAtUtc <= now)
            .Take(20)
            .ToListAsync();

        _logger.LogInformation("Encontrados {Count} posts listos para publicar", postsToPublish.Count);

        foreach (var post in postsToPublish)
        {
            await PublishPostAsync(post);
        }
    }

    private async Task PublishPostAsync(BasePost post)
    {
        var successCount = 0;
        var failCount = 0;

        foreach (var adaptedPost in post.AdaptedVersions.Where(ap => ap.State == AdaptedPostState.Ready))
        {
            var result = await PublishToNetworkAsync(adaptedPost.Id);

            if (result.Success)
            {
                successCount++;
            }
            else
            {
                failCount++;
                _logger.LogWarning(
                    "Fallo publicando en {Network}: {Error}",
                    adaptedPost.NetworkType, result.ErrorMessage);
            }
        }

        // Actualizar estado del post base
        if (failCount == 0 && successCount > 0)
        {
            post.State = BasePostState.Publicada;
            post.PublishedAt = DateTime.UtcNow;
        }
        else if (successCount > 0)
        {
            post.State = BasePostState.ParcialmentePublicada;
        }

        await _context.SaveChangesAsync();
    }

    public async Task<PublishResult> PublishToNetworkAsync(Guid adaptedPostId)
    {
        var adaptedPost = await _context.AdaptedPosts
            .Include(ap => ap.BasePost)
                .ThenInclude(bp => bp.Account)
                    .ThenInclude(a => a.SocialChannels)
            .Include(ap => ap.BasePost)
                .ThenInclude(bp => bp.Media)
            .FirstOrDefaultAsync(ap => ap.Id == adaptedPostId)
            ?? throw new InvalidOperationException($"Post adaptado no encontrado: {adaptedPostId}");

        var channelConfig = adaptedPost.BasePost.Account.SocialChannels
            .FirstOrDefault(c => c.NetworkType == adaptedPost.NetworkType && c.IsEnabled);

        if (channelConfig == null)
        {
            return new PublishResult
            {
                Success = false,
                ErrorMessage = $"No hay canal configurado para {adaptedPost.NetworkType}"
            };
        }

        if (channelConfig.HealthStatus == HealthStatus.KO)
        {
            return new PublishResult
            {
                Success = false,
                ErrorMessage = $"El canal {adaptedPost.NetworkType} esta en estado de error"
            };
        }

        try
        {
            string externalId;

            // Obtener URLs de media si existen
            var mediaUrls = adaptedPost.BasePost.Media?
                .Where(m => !string.IsNullOrEmpty(m.Url))
                .Select(m => m.Url!)
                .ToList() ?? new List<string>();

            // Publicar segun la red y el metodo de autenticacion
            if (channelConfig.AuthMethod == AuthMethod.ApiKey)
            {
                var credentials = await _channelConfigService.GetDecryptedApiKeyCredentialsAsync(channelConfig.Id);
                if (credentials == null)
                {
                    throw new InvalidOperationException("No se pudieron obtener las credenciales de API");
                }

                externalId = adaptedPost.NetworkType switch
                {
                    NetworkType.X => await PublishToXWithRefitAsync(adaptedPost, credentials.Value),
                    _ => throw new NotSupportedException($"Red {adaptedPost.NetworkType} no soporta ApiKey")
                };
            }
            else
            {
                var credentials = await _channelConfigService.GetDecryptedOAuthCredentialsAsync(channelConfig.Id);
                if (credentials == null)
                {
                    throw new InvalidOperationException("No se pudieron obtener las credenciales de OAuth");
                }

                externalId = adaptedPost.NetworkType switch
                {
                    NetworkType.Facebook => await PublishToFacebookWithRefitAsync(adaptedPost, credentials.Value.AccessToken, mediaUrls),
                    NetworkType.Instagram => await PublishToInstagramWithRefitAsync(adaptedPost, credentials.Value.AccessToken, mediaUrls),
                    NetworkType.LinkedIn => await PublishToLinkedInAsync(adaptedPost, credentials.Value.AccessToken),
                    NetworkType.TikTok => await PublishToTikTokWithRefitAsync(adaptedPost, credentials.Value.AccessToken, mediaUrls),
                    NetworkType.YouTube => await PublishToYouTubeWithSdkAsync(adaptedPost, credentials.Value.AccessToken),
                    _ => throw new NotSupportedException($"Red no soportada: {adaptedPost.NetworkType}")
                };
            }

            // Actualizar post adaptado
            adaptedPost.State = AdaptedPostState.Published;
            adaptedPost.PublishedAt = DateTime.UtcNow;
            adaptedPost.ExternalPostId = externalId;

            // Actualizar health status a OK
            await _channelConfigService.UpdateHealthStatusAsync(channelConfig.Id, HealthStatus.OK);

            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Post publicado exitosamente en {Network}: {ExternalId}",
                adaptedPost.NetworkType, externalId);

            return new PublishResult { Success = true, ExternalId = externalId };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error publicando post {PostId} en {Network}",
                adaptedPostId, adaptedPost.NetworkType);

            adaptedPost.State = AdaptedPostState.Failed;
            adaptedPost.LastError = ex.Message;
            adaptedPost.RetryCount++;

            await _context.SaveChangesAsync();

            // Actualizar health status del canal
            await _channelConfigService.UpdateHealthStatusAsync(
                channelConfig.Id, HealthStatus.KO, ex.Message);

            return new PublishResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    public async Task RetryFailedPublicationsAsync()
    {
        var failedPosts = await _context.AdaptedPosts
            .Where(ap => ap.State == AdaptedPostState.Failed && ap.RetryCount < 3)
            .Take(10)
            .ToListAsync();

        foreach (var post in failedPosts)
        {
            await PublishToNetworkAsync(post.Id);
        }
    }

    #region X (Twitter) - Refit con OAuth 1.0a

    private async Task<string> PublishToXWithRefitAsync(
        AdaptedPost post,
        (string ApiKey, string ApiSecret, string AccessToken, string AccessTokenSecret) credentials)
    {
        _logger.LogInformation("Publicando en X (Twitter) con Refit...");

        // Generar header OAuth 1.0a
        var authHeader = OAuth1Helper.GenerateOAuth1Header(
            "POST",
            "https://api.x.com/2/tweets",
            credentials.ApiKey,
            credentials.ApiSecret,
            credentials.AccessToken,
            credentials.AccessTokenSecret);

        var request = new XTweetRequest
        {
            Text = post.AdaptedContent
        };

        try
        {
            var response = await _xApiClient.CreateTweetAsync(request, authHeader);

            return response.Data?.Id
                ?? throw new Exception("No se recibió ID del tweet");
        }
        catch (Refit.ApiException ex)
        {
            var errorContent = await ex.GetContentAsAsync<string>();
            _logger.LogError("Error de X/Twitter API: {StatusCode} - {Content}",
                ex.StatusCode, errorContent);
            throw new Exception($"Error de X/Twitter: {ex.StatusCode} - {errorContent}");
        }
    }

    #endregion

    #region Facebook - Refit

    private async Task<string> PublishToFacebookWithRefitAsync(
        AdaptedPost post,
        string accessToken,
        List<string> mediaUrls)
    {
        _logger.LogInformation("Publicando en Facebook con Refit...");

        try
        {
            // Obtener páginas del usuario
            var pagesResponse = await _metaApiClient.GetUserPagesAsync(accessToken);
            var page = pagesResponse.Data?.FirstOrDefault()
                ?? throw new Exception("No se encontraron páginas de Facebook");

            // Usar el token de la página si está disponible
            var pageToken = page.AccessToken ?? accessToken;

            // Si hay imágenes, publicar con imagen
            if (mediaUrls.Any())
            {
                var response = await _metaApiClient.CreateFacebookPhotoPostAsync(
                    page.Id!,
                    post.AdaptedContent,
                    mediaUrls.First(),
                    pageToken);

                return response.Id
                    ?? throw new Exception("No se recibió ID del post de Facebook");
            }
            else
            {
                // Publicar solo texto
                var response = await _metaApiClient.CreateFacebookPostAsync(
                    page.Id!,
                    post.AdaptedContent,
                    pageToken);

                return response.Id
                    ?? throw new Exception("No se recibió ID del post de Facebook");
            }
        }
        catch (Refit.ApiException ex)
        {
            var errorContent = await ex.GetContentAsAsync<string>();
            _logger.LogError("Error de Facebook API: {StatusCode} - {Content}",
                ex.StatusCode, errorContent);
            throw new Exception($"Error de Facebook: {ex.StatusCode} - {errorContent}");
        }
    }

    #endregion

    #region Instagram - Refit

    private async Task<string> PublishToInstagramWithRefitAsync(
        AdaptedPost post,
        string accessToken,
        List<string> mediaUrls)
    {
        _logger.LogInformation("Publicando en Instagram con Refit...");

        try
        {
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

            // Instagram REQUIERE contenido multimedia
            if (!mediaUrls.Any())
            {
                _logger.LogWarning("Instagram requiere imagen. Publicación de solo texto no soportada.");
                return $"ig_no_media_{Guid.NewGuid():N}";
            }

            var imageUrl = mediaUrls.First();

            // Step 1: Crear contenedor de media
            var containerResponse = await _metaApiClient.CreateInstagramMediaContainerAsync(
                igAccountId,
                imageUrl,
                null,  // videoUrl
                post.AdaptedContent,
                null,  // mediaType (null para imagen)
                accessToken);

            var containerId = containerResponse.Id
                ?? throw new Exception("No se pudo crear contenedor de Instagram");

            // Step 2: Verificar estado del contenedor
            var maxRetries = 10;
            for (int i = 0; i < maxRetries; i++)
            {
                var statusResponse = await _metaApiClient.GetContainerStatusAsync(
                    containerId,
                    "status_code",
                    accessToken);

                if (statusResponse.StatusCode == "FINISHED")
                {
                    break;
                }

                if (statusResponse.StatusCode == "ERROR")
                {
                    throw new Exception("Error procesando media de Instagram");
                }

                // Esperar antes de reintentar
                await Task.Delay(2000);
            }

            // Step 3: Publicar
            var publishResponse = await _metaApiClient.PublishInstagramMediaAsync(
                igAccountId,
                containerId,
                accessToken);

            return publishResponse.Id
                ?? throw new Exception("No se recibió ID del post de Instagram");
        }
        catch (Refit.ApiException ex)
        {
            var errorContent = await ex.GetContentAsAsync<string>();
            _logger.LogError("Error de Instagram API: {StatusCode} - {Content}",
                ex.StatusCode, errorContent);
            throw new Exception($"Error de Instagram: {ex.StatusCode} - {errorContent}");
        }
    }

    #endregion

    #region TikTok - Refit

    private async Task<string> PublishToTikTokWithRefitAsync(
        AdaptedPost post,
        string accessToken,
        List<string> mediaUrls)
    {
        _logger.LogInformation("Publicando en TikTok con Refit...");

        // TikTok requiere contenido multimedia
        if (!mediaUrls.Any())
        {
            _logger.LogWarning("TikTok requiere contenido multimedia");
            return $"tt_no_media_{Guid.NewGuid():N}";
        }

        try
        {
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
                    PhotoImages = mediaUrls,
                    PhotoCoverIndex = "0"
                }
            };

            var authHeader = $"Bearer {accessToken}";
            var response = await _tikTokApiClient.InitPhotoPublishAsync(request, authHeader);

            if (response.Error != null)
            {
                throw new Exception($"Error de TikTok: {response.Error.Message} (Code: {response.Error.Code})");
            }

            var publishId = response.Data?.PublishId
                ?? throw new Exception("No se recibió PublishId de TikTok");

            // Polling para verificar estado (máximo 30 segundos)
            for (int i = 0; i < 10; i++)
            {
                await Task.Delay(3000);

                var statusResponse = await _tikTokApiClient.GetPublishStatusAsync(
                    new TikTokStatusRequest { PublishId = publishId },
                    authHeader);

                if (statusResponse.Data?.Status == "PUBLISH_COMPLETE")
                {
                    return statusResponse.Data.PubliclyAvailablePostId ?? publishId;
                }

                if (statusResponse.Data?.Status == "FAILED")
                {
                    var reasons = statusResponse.Data.FailReason != null
                        ? string.Join(", ", statusResponse.Data.FailReason)
                        : "Unknown error";
                    throw new Exception($"Publicación de TikTok falló: {reasons}");
                }
            }

            // Si después de 30 segundos sigue procesando, devolver el publishId
            _logger.LogWarning("TikTok sigue procesando después de 30s, devolviendo publishId");
            return publishId;
        }
        catch (Refit.ApiException ex)
        {
            var errorContent = await ex.GetContentAsAsync<string>();
            _logger.LogError("Error de TikTok API: {StatusCode} - {Content}",
                ex.StatusCode, errorContent);
            throw new Exception($"Error de TikTok: {ex.StatusCode} - {errorContent}");
        }
    }

    #endregion

    #region YouTube - SDK Oficial de Google

    private async Task<string> PublishToYouTubeWithSdkAsync(AdaptedPost post, string accessToken)
    {
        _logger.LogInformation("Publicando en YouTube con SDK oficial...");

        // YouTube requiere video para publicar
        var videoMedia = post.BasePost?.Media?
            .FirstOrDefault(m => m.IsVideo);

        if (videoMedia == null || string.IsNullOrEmpty(videoMedia.RelativePath))
        {
            _logger.LogWarning("YouTube requiere contenido de video. Publicación de solo texto/imagen no soportada.");
            return $"yt_video_required_{Guid.NewGuid():N}";
        }

        try
        {
            // Construir la ruta física completa desde RelativePath y StorageSettings
            var physicalPath = Path.Combine(
                _storageSettings.UploadsPath,
                videoMedia.RelativePath.Replace('/', Path.DirectorySeparatorChar));

            if (!File.Exists(physicalPath))
            {
                _logger.LogError("Archivo de video no encontrado: {Path}", physicalPath);
                throw new FileNotFoundException($"Archivo de video no encontrado: {physicalPath}");
            }

            // Leer el archivo de video
            using var videoStream = File.OpenRead(physicalPath);

            var videoId = await _youTubeService.UploadVideoAsync(
                accessToken,
                videoStream,
                post.BasePost?.Title ?? "Sin título",
                post.AdaptedContent,
                post.BasePost?.Title?.Split(' ') ?? Array.Empty<string>(),
                "public");

            _logger.LogInformation("Video subido a YouTube: {VideoId}", videoId);
            return videoId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error subiendo video a YouTube");
            throw new Exception($"Error de YouTube: {ex.Message}");
        }
    }

    #endregion

    #region LinkedIn - HttpClient (sin cambios)

    private async Task<string> PublishToLinkedInAsync(AdaptedPost post, string accessToken)
    {
        _logger.LogInformation("Publicando en LinkedIn...");

        using var client = _httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        // Obtener URN del usuario
        var userResponse = await client.GetAsync("https://api.linkedin.com/v2/userinfo");
        if (!userResponse.IsSuccessStatusCode)
        {
            throw new Exception("Error obteniendo informacion del usuario de LinkedIn");
        }

        var userData = await userResponse.Content.ReadFromJsonAsync<LinkedInUserInfo>();
        var authorUrn = $"urn:li:person:{userData?.Sub}";

        // Crear post
        var postData = new
        {
            author = authorUrn,
            lifecycleState = "PUBLISHED",
            specificContent = new
            {
                @shareContent = new
                {
                    shareCommentary = new { text = post.AdaptedContent },
                    shareMediaCategory = "NONE"
                }
            },
            visibility = new
            {
                @memberNetworkVisibility = "PUBLIC"
            }
        };

        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        var content = new StringContent(
            JsonSerializer.Serialize(postData, jsonOptions),
            Encoding.UTF8,
            "application/json");

        content.Headers.Add("X-Restli-Protocol-Version", "2.0.0");

        var response = await client.PostAsync("https://api.linkedin.com/v2/ugcPosts", content);
        var responseContent = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            throw new Exception($"Error publicando en LinkedIn: {responseContent}");
        }

        var result = JsonSerializer.Deserialize<LinkedInPostResponse>(responseContent);
        return result?.Id ?? $"li_{Guid.NewGuid():N}";
    }

    #endregion

    #region Response Models para LinkedIn

    private class LinkedInUserInfo
    {
        [JsonPropertyName("sub")]
        public string? Sub { get; set; }
    }

    private class LinkedInPostResponse
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }
    }

    #endregion
}
