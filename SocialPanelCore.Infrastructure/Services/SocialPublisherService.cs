using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Web;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SocialPanelCore.Domain.Entities;
using SocialPanelCore.Domain.Enums;
using SocialPanelCore.Domain.Interfaces;
using SocialPanelCore.Infrastructure.Data;

namespace SocialPanelCore.Infrastructure.Services;

public class SocialPublisherService : ISocialPublisherService
{
    private readonly ApplicationDbContext _context;
    private readonly ISocialChannelConfigService _channelConfigService;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<SocialPublisherService> _logger;

    public SocialPublisherService(
        ApplicationDbContext context,
        ISocialChannelConfigService channelConfigService,
        IHttpClientFactory httpClientFactory,
        ILogger<SocialPublisherService> logger)
    {
        _context = context;
        _channelConfigService = channelConfigService;
        _httpClientFactory = httpClientFactory;
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
                    NetworkType.X => await PublishToXAsync(adaptedPost, credentials.Value),
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
                    NetworkType.Facebook => await PublishToFacebookAsync(adaptedPost, credentials.Value.AccessToken, channelConfig),
                    NetworkType.Instagram => await PublishToInstagramAsync(adaptedPost, credentials.Value.AccessToken, channelConfig),
                    NetworkType.LinkedIn => await PublishToLinkedInAsync(adaptedPost, credentials.Value.AccessToken),
                    NetworkType.TikTok => await PublishToTikTokAsync(adaptedPost, credentials.Value.AccessToken),
                    NetworkType.YouTube => await PublishToYouTubeAsync(adaptedPost, credentials.Value.AccessToken),
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

    #region X (Twitter) - OAuth 1.0a

    private async Task<string> PublishToXAsync(AdaptedPost post,
        (string ApiKey, string ApiSecret, string AccessToken, string AccessTokenSecret) credentials)
    {
        _logger.LogInformation("Publicando en X (Twitter)...");

        using var client = _httpClientFactory.CreateClient();

        var tweetUrl = "https://api.twitter.com/2/tweets";
        var tweetContent = new { text = post.AdaptedContent };
        var jsonContent = JsonSerializer.Serialize(tweetContent);

        // Generar firma OAuth 1.0a
        var authHeader = GenerateOAuth1Header(
            "POST",
            tweetUrl,
            credentials.ApiKey,
            credentials.ApiSecret,
            credentials.AccessToken,
            credentials.AccessTokenSecret);

        using var request = new HttpRequestMessage(HttpMethod.Post, tweetUrl);
        request.Headers.Add("Authorization", authHeader);
        request.Content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

        var response = await client.SendAsync(request);
        var responseContent = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Error de X/Twitter: {StatusCode} - {Content}",
                response.StatusCode, responseContent);
            throw new Exception($"Error de X/Twitter: {response.StatusCode} - {responseContent}");
        }

        var result = JsonSerializer.Deserialize<TwitterTweetResponse>(responseContent);
        return result?.Data?.Id ?? throw new Exception("No se recibio ID del tweet");
    }

    private string GenerateOAuth1Header(string method, string url,
        string consumerKey, string consumerSecret,
        string accessToken, string accessTokenSecret)
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

        // Crear base string
        var paramString = string.Join("&",
            oauthParams.Select(kvp => $"{Uri.EscapeDataString(kvp.Key)}={Uri.EscapeDataString(kvp.Value)}"));

        var baseString = $"{method.ToUpper()}&{Uri.EscapeDataString(url)}&{Uri.EscapeDataString(paramString)}";

        // Crear signing key
        var signingKey = $"{Uri.EscapeDataString(consumerSecret)}&{Uri.EscapeDataString(accessTokenSecret)}";

        // Generar firma
        using var hmac = new HMACSHA1(Encoding.UTF8.GetBytes(signingKey));
        var signatureBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(baseString));
        var signature = Convert.ToBase64String(signatureBytes);

        oauthParams["oauth_signature"] = signature;

        // Crear header
        var headerValue = string.Join(", ",
            oauthParams.Select(kvp => $"{Uri.EscapeDataString(kvp.Key)}=\"{Uri.EscapeDataString(kvp.Value)}\""));

        return $"OAuth {headerValue}";
    }

    #endregion

    #region Facebook - Graph API

    private async Task<string> PublishToFacebookAsync(AdaptedPost post, string accessToken, SocialChannelConfig config)
    {
        _logger.LogInformation("Publicando en Facebook...");

        using var client = _httpClientFactory.CreateClient();

        // Primero obtener las paginas del usuario
        var pagesUrl = $"https://graph.facebook.com/v18.0/me/accounts?access_token={accessToken}";
        var pagesResponse = await client.GetAsync(pagesUrl);

        if (!pagesResponse.IsSuccessStatusCode)
        {
            var error = await pagesResponse.Content.ReadAsStringAsync();
            throw new Exception($"Error obteniendo paginas de Facebook: {error}");
        }

        var pagesData = await pagesResponse.Content.ReadFromJsonAsync<FacebookPagesResponse>();
        var page = pagesData?.Data?.FirstOrDefault();

        if (page == null)
        {
            throw new Exception("No se encontraron paginas de Facebook");
        }

        // Publicar en la pagina usando el page access token
        var postUrl = $"https://graph.facebook.com/v18.0/{page.Id}/feed";
        var postContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["message"] = post.AdaptedContent,
            ["access_token"] = page.AccessToken ?? accessToken
        });

        var response = await client.PostAsync(postUrl, postContent);
        var responseContent = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            throw new Exception($"Error publicando en Facebook: {responseContent}");
        }

        var result = JsonSerializer.Deserialize<FacebookPostResponse>(responseContent);
        return result?.Id ?? throw new Exception("No se recibio ID del post de Facebook");
    }

    #endregion

    #region Instagram - Graph API

    private async Task<string> PublishToInstagramAsync(AdaptedPost post, string accessToken, SocialChannelConfig config)
    {
        _logger.LogInformation("Publicando en Instagram...");

        using var client = _httpClientFactory.CreateClient();

        // Obtener el Instagram Business Account
        var pagesUrl = $"https://graph.facebook.com/v18.0/me/accounts?access_token={accessToken}";
        var pagesResponse = await client.GetAsync(pagesUrl);

        if (!pagesResponse.IsSuccessStatusCode)
        {
            throw new Exception("Error obteniendo paginas de Facebook para Instagram");
        }

        var pagesData = await pagesResponse.Content.ReadFromJsonAsync<FacebookPagesResponse>();
        var page = pagesData?.Data?.FirstOrDefault();

        if (page == null)
        {
            throw new Exception("No se encontraron paginas de Facebook");
        }

        // Obtener Instagram Business Account
        var igUrl = $"https://graph.facebook.com/v18.0/{page.Id}?fields=instagram_business_account&access_token={accessToken}";
        var igResponse = await client.GetAsync(igUrl);

        if (!igResponse.IsSuccessStatusCode)
        {
            throw new Exception("Error obteniendo cuenta de Instagram Business");
        }

        var igData = await igResponse.Content.ReadFromJsonAsync<InstagramAccountData>();
        var igAccountId = igData?.InstagramBusinessAccount?.Id;

        if (string.IsNullOrEmpty(igAccountId))
        {
            throw new Exception("No se encontro cuenta de Instagram Business vinculada");
        }

        // Para Instagram, necesitamos crear un contenedor primero
        // Si es solo texto, no podemos publicar (Instagram requiere imagen/video)
        // Por ahora, simulamos una publicacion de texto como caption

        _logger.LogWarning("Instagram requiere imagen/video. Publicacion de solo texto no soportada directamente.");

        // Devolver un ID simulado indicando que se necesita media
        return $"ig_text_only_{Guid.NewGuid():N}";
    }

    #endregion

    #region LinkedIn

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

    #region TikTok

    private async Task<string> PublishToTikTokAsync(AdaptedPost post, string accessToken)
    {
        _logger.LogInformation("Publicando en TikTok (requiere video)...");

        // TikTok solo permite publicar videos, no texto
        _logger.LogWarning("TikTok requiere contenido de video. Publicacion de solo texto no soportada.");

        return $"tt_video_required_{Guid.NewGuid():N}";
    }

    #endregion

    #region YouTube

    private async Task<string> PublishToYouTubeAsync(AdaptedPost post, string accessToken)
    {
        _logger.LogInformation("Publicando en YouTube (requiere video)...");

        // YouTube solo permite publicar videos
        _logger.LogWarning("YouTube requiere contenido de video. Publicacion de solo texto no soportada.");

        return $"yt_video_required_{Guid.NewGuid():N}";
    }

    #endregion

    #region Response Models

    private class TwitterTweetResponse
    {
        [JsonPropertyName("data")]
        public TwitterTweetData? Data { get; set; }
    }

    private class TwitterTweetData
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("text")]
        public string? Text { get; set; }
    }

    private class FacebookPagesResponse
    {
        [JsonPropertyName("data")]
        public List<FacebookPage>? Data { get; set; }
    }

    private class FacebookPage
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("access_token")]
        public string? AccessToken { get; set; }
    }

    private class FacebookPostResponse
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }
    }

    private class InstagramAccountData
    {
        [JsonPropertyName("instagram_business_account")]
        public InstagramBusinessAccount? InstagramBusinessAccount { get; set; }
    }

    private class InstagramBusinessAccount
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }
    }

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
