using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Web;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SocialPanelCore.Domain.Enums;
using SocialPanelCore.Domain.Interfaces;

namespace SocialPanelCore.Infrastructure.Services;

/// <summary>
/// Servicio para manejar flujos OAuth con redes sociales.
/// Soporta Facebook, Instagram (via Meta) y X (Twitter) con PKCE.
/// </summary>
public class OAuthService : IOAuthService
{
    private readonly IConfiguration _configuration;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<OAuthService> _logger;

    // Scopes por defecto para cada proveedor
    private static readonly Dictionary<NetworkType, string> DefaultScopes = new()
    {
        [NetworkType.Facebook] = "pages_manage_posts,pages_read_engagement,pages_show_list,public_profile",
        [NetworkType.Instagram] = "instagram_business_basic,instagram_business_manage_messages,instagram_business_manage_comments,instagram_business_content_publish",
        [NetworkType.X] = "tweet.read,tweet.write,users.read,offline.access",
        [NetworkType.LinkedIn] = "openid,profile,w_member_social",
        [NetworkType.TikTok] = "user.info.basic,video.publish,video.upload",
        [NetworkType.YouTube] = "https://www.googleapis.com/auth/youtube.upload https://www.googleapis.com/auth/youtube.readonly https://www.googleapis.com/auth/userinfo.profile"
    };

    public OAuthService(
        IConfiguration configuration,
        IHttpClientFactory httpClientFactory,
        ILogger<OAuthService> logger)
    {
        _configuration = configuration;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public bool RequiresPkce(NetworkType networkType)
    {
        // X (Twitter) y TikTok requieren PKCE obligatoriamente
        return networkType == NetworkType.X || networkType == NetworkType.TikTok;
    }

    public string GetDefaultScopes(NetworkType networkType)
    {
        return DefaultScopes.TryGetValue(networkType, out var scopes) ? scopes : string.Empty;
    }

    public string GetAuthorizationUrl(NetworkType networkType, string state, string redirectUri, string? codeChallenge = null)
    {
        return networkType switch
        {
            NetworkType.Facebook => GetFacebookAuthUrl(state, redirectUri),
            NetworkType.Instagram => GetInstagramAuthUrl(state, redirectUri),
            NetworkType.X => GetXAuthUrl(state, redirectUri, codeChallenge),
            NetworkType.LinkedIn => GetLinkedInAuthUrl(state, redirectUri),
            NetworkType.TikTok => GetTikTokAuthUrl(state, redirectUri, codeChallenge),
            NetworkType.YouTube => GetYouTubeAuthUrl(state, redirectUri),
            _ => throw new NotSupportedException($"OAuth no soportado para {networkType}")
        };
    }

    public async Task<OAuthTokenResult> ExchangeCodeForTokensAsync(
        NetworkType networkType,
        string code,
        string redirectUri,
        string? codeVerifier = null)
    {
        try
        {
            return networkType switch
            {
                NetworkType.Facebook => await ExchangeFacebookCodeAsync(code, redirectUri),
                NetworkType.Instagram => await ExchangeInstagramCodeAsync(code, redirectUri),
                NetworkType.X => await ExchangeXCodeAsync(code, redirectUri, codeVerifier),
                NetworkType.LinkedIn => await ExchangeLinkedInCodeAsync(code, redirectUri),
                NetworkType.TikTok => await ExchangeTikTokCodeAsync(code, redirectUri, codeVerifier),
                NetworkType.YouTube => await ExchangeYouTubeCodeAsync(code, redirectUri),
                _ => new OAuthTokenResult { Success = false, Error = $"OAuth no soportado para {networkType}" }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error intercambiando codigo OAuth para {NetworkType}", networkType);
            return new OAuthTokenResult
            {
                Success = false,
                Error = "exchange_error",
                ErrorDescription = ex.Message
            };
        }
    }

    public async Task<OAuthTokenResult> RefreshTokenAsync(NetworkType networkType, string refreshToken)
    {
        try
        {
            return networkType switch
            {
                NetworkType.Facebook => await RefreshFacebookTokenAsync(refreshToken),
                NetworkType.Instagram => await RefreshInstagramTokenAsync(refreshToken),
                NetworkType.X => await RefreshXTokenAsync(refreshToken),
                NetworkType.LinkedIn => await RefreshLinkedInTokenAsync(refreshToken),
                NetworkType.TikTok => await RefreshTikTokTokenAsync(refreshToken),
                NetworkType.YouTube => await RefreshYouTubeTokenAsync(refreshToken),
                _ => new OAuthTokenResult { Success = false, Error = $"Refresh no soportado para {networkType}" }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refrescando token para {NetworkType}", networkType);
            return new OAuthTokenResult
            {
                Success = false,
                Error = "refresh_error",
                ErrorDescription = ex.Message
            };
        }
    }

    public async Task<OAuthUserInfo?> GetUserInfoAsync(NetworkType networkType, string accessToken)
    {
        try
        {
            return networkType switch
            {
                NetworkType.Facebook => await GetFacebookUserInfoAsync(accessToken),
                NetworkType.Instagram => await GetInstagramUserInfoAsync(accessToken),
                NetworkType.X => await GetXUserInfoAsync(accessToken),
                NetworkType.LinkedIn => await GetLinkedInUserInfoAsync(accessToken),
                NetworkType.TikTok => await GetTikTokUserInfoAsync(accessToken),
                NetworkType.YouTube => await GetYouTubeUserInfoAsync(accessToken),
                _ => null
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error obteniendo info de usuario para {NetworkType}", networkType);
            return null;
        }
    }

    public async Task<bool> RevokeTokenAsync(NetworkType networkType, string accessToken)
    {
        try
        {
            return networkType switch
            {
                NetworkType.X => await RevokeXTokenAsync(accessToken),
                NetworkType.YouTube => await RevokeYouTubeTokenAsync(accessToken),
                NetworkType.TikTok => await RevokeTikTokTokenAsync(accessToken),
                // Facebook/Instagram/LinkedIn no tienen endpoint de revoke directo,
                // se hace desde la configuración de la app del usuario
                _ => false
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error revocando token para {NetworkType}", networkType);
            return false;
        }
    }

    #region Facebook

    private string GetFacebookAuthUrl(string state, string redirectUri)
    {
        var appId = _configuration["OAuth:Facebook:AppId"];
        var scopes = DefaultScopes[NetworkType.Facebook];

        return $"https://www.facebook.com/v18.0/dialog/oauth" +
               $"?client_id={appId}" +
               $"&redirect_uri={HttpUtility.UrlEncode(redirectUri)}" +
               $"&scope={HttpUtility.UrlEncode(scopes)}" +
               $"&state={HttpUtility.UrlEncode(state)}" +
               $"&response_type=code";
    }

    private async Task<OAuthTokenResult> ExchangeFacebookCodeAsync(string code, string redirectUri)
    {
        var appId = _configuration["OAuth:Facebook:AppId"];
        var appSecret = _configuration["OAuth:Facebook:AppSecret"];

        using var client = _httpClientFactory.CreateClient();

        // Paso 1: Intercambiar código por short-lived token
        var url = $"https://graph.facebook.com/v18.0/oauth/access_token" +
                  $"?client_id={appId}" +
                  $"&client_secret={appSecret}" +
                  $"&code={code}" +
                  $"&redirect_uri={HttpUtility.UrlEncode(redirectUri)}";

        var response = await client.GetAsync(url);
        var content = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Error en Facebook OAuth: {Content}", content);
            return new OAuthTokenResult { Success = false, Error = "facebook_error", ErrorDescription = content };
        }

        var shortLivedToken = JsonSerializer.Deserialize<FacebookTokenResponse>(content);

        // Paso 2: Intercambiar por long-lived token (~60 días)
        var longLivedUrl = $"https://graph.facebook.com/v18.0/oauth/access_token" +
                          $"?grant_type=fb_exchange_token" +
                          $"&client_id={appId}" +
                          $"&client_secret={appSecret}" +
                          $"&fb_exchange_token={shortLivedToken?.AccessToken}";

        var longLivedResponse = await client.GetAsync(longLivedUrl);
        var longLivedContent = await longLivedResponse.Content.ReadAsStringAsync();

        if (!longLivedResponse.IsSuccessStatusCode)
        {
            _logger.LogWarning("Error obteniendo long-lived token: {Content}", longLivedContent);
            // Devolver el short-lived si falla el long-lived
            return new OAuthTokenResult
            {
                Success = true,
                AccessToken = shortLivedToken?.AccessToken,
                ExpiresAt = shortLivedToken?.ExpiresIn > 0
                    ? DateTime.UtcNow.AddSeconds(shortLivedToken.ExpiresIn)
                    : null,
                Scopes = DefaultScopes[NetworkType.Facebook]
            };
        }

        var longLivedTokenResponse = JsonSerializer.Deserialize<FacebookTokenResponse>(longLivedContent);

        return new OAuthTokenResult
        {
            Success = true,
            AccessToken = longLivedTokenResponse?.AccessToken,
            ExpiresAt = longLivedTokenResponse?.ExpiresIn > 0
                ? DateTime.UtcNow.AddSeconds(longLivedTokenResponse.ExpiresIn)
                : DateTime.UtcNow.AddDays(60), // Default 60 días para long-lived
            Scopes = DefaultScopes[NetworkType.Facebook]
        };
    }

    private async Task<OAuthTokenResult> RefreshFacebookTokenAsync(string currentToken)
    {
        var appId = _configuration["OAuth:Facebook:AppId"];
        var appSecret = _configuration["OAuth:Facebook:AppSecret"];

        using var client = _httpClientFactory.CreateClient();

        // Facebook usa fb_exchange_token para "refrescar" (en realidad extiende)
        var url = $"https://graph.facebook.com/v18.0/oauth/access_token" +
                  $"?grant_type=fb_exchange_token" +
                  $"&client_id={appId}" +
                  $"&client_secret={appSecret}" +
                  $"&fb_exchange_token={currentToken}";

        var response = await client.GetAsync(url);
        var content = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            var errorResponse = JsonSerializer.Deserialize<FacebookErrorResponse>(content);
            return new OAuthTokenResult
            {
                Success = false,
                Error = errorResponse?.Error?.Code?.ToString() ?? "refresh_error",
                ErrorDescription = errorResponse?.Error?.Message ?? content
            };
        }

        var tokenResponse = JsonSerializer.Deserialize<FacebookTokenResponse>(content);

        return new OAuthTokenResult
        {
            Success = true,
            AccessToken = tokenResponse?.AccessToken,
            ExpiresAt = tokenResponse?.ExpiresIn > 0
                ? DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn)
                : DateTime.UtcNow.AddDays(60)
        };
    }

    private async Task<OAuthUserInfo?> GetFacebookUserInfoAsync(string accessToken)
    {
        using var client = _httpClientFactory.CreateClient();
        var url = $"https://graph.facebook.com/v18.0/me?fields=id,name&access_token={accessToken}";

        var response = await client.GetAsync(url);
        if (!response.IsSuccessStatusCode) return null;

        var user = await response.Content.ReadFromJsonAsync<FacebookUserResponse>();
        return new OAuthUserInfo
        {
            Id = user?.Id,
            DisplayName = user?.Name,
            Username = user?.Name
        };
    }

    #endregion

    #region Instagram

    private string GetInstagramAuthUrl(string state, string redirectUri)
    {
        var appId = _configuration["OAuth:Instagram:AppId"];
        var scopes = DefaultScopes[NetworkType.Instagram];

        // Instagram Business usa el mismo endpoint de Facebook
        return $"https://www.facebook.com/v18.0/dialog/oauth" +
               $"?client_id={appId}" +
               $"&redirect_uri={HttpUtility.UrlEncode(redirectUri)}" +
               $"&scope={HttpUtility.UrlEncode(scopes)}" +
               $"&state={HttpUtility.UrlEncode(state)}" +
               $"&response_type=code";
    }

    private async Task<OAuthTokenResult> ExchangeInstagramCodeAsync(string code, string redirectUri)
    {
        // Instagram Business usa la misma API que Facebook (Meta Graph API)
        // Usamos las credenciales de Instagram pero el mismo flujo
        var appId = _configuration["OAuth:Instagram:AppId"];
        var appSecret = _configuration["OAuth:Instagram:AppSecret"];

        using var client = _httpClientFactory.CreateClient();

        var url = $"https://graph.facebook.com/v18.0/oauth/access_token" +
                  $"?client_id={appId}" +
                  $"&client_secret={appSecret}" +
                  $"&code={code}" +
                  $"&redirect_uri={HttpUtility.UrlEncode(redirectUri)}";

        var response = await client.GetAsync(url);
        var content = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Error en Instagram OAuth: {Content}", content);
            return new OAuthTokenResult { Success = false, Error = "instagram_error", ErrorDescription = content };
        }

        var shortLivedToken = JsonSerializer.Deserialize<FacebookTokenResponse>(content);

        // Obtener long-lived token
        var longLivedUrl = $"https://graph.facebook.com/v18.0/oauth/access_token" +
                          $"?grant_type=fb_exchange_token" +
                          $"&client_id={appId}" +
                          $"&client_secret={appSecret}" +
                          $"&fb_exchange_token={shortLivedToken?.AccessToken}";

        var longLivedResponse = await client.GetAsync(longLivedUrl);
        var longLivedContent = await longLivedResponse.Content.ReadAsStringAsync();

        if (!longLivedResponse.IsSuccessStatusCode)
        {
            return new OAuthTokenResult
            {
                Success = true,
                AccessToken = shortLivedToken?.AccessToken,
                ExpiresAt = shortLivedToken?.ExpiresIn > 0
                    ? DateTime.UtcNow.AddSeconds(shortLivedToken.ExpiresIn)
                    : null,
                Scopes = DefaultScopes[NetworkType.Instagram]
            };
        }

        var longLivedTokenResponse = JsonSerializer.Deserialize<FacebookTokenResponse>(longLivedContent);

        return new OAuthTokenResult
        {
            Success = true,
            AccessToken = longLivedTokenResponse?.AccessToken,
            ExpiresAt = longLivedTokenResponse?.ExpiresIn > 0
                ? DateTime.UtcNow.AddSeconds(longLivedTokenResponse.ExpiresIn)
                : DateTime.UtcNow.AddDays(60),
            Scopes = DefaultScopes[NetworkType.Instagram]
        };
    }

    private async Task<OAuthTokenResult> RefreshInstagramTokenAsync(string currentToken)
    {
        // Instagram usa el mismo método que Facebook
        var appId = _configuration["OAuth:Instagram:AppId"];
        var appSecret = _configuration["OAuth:Instagram:AppSecret"];

        using var client = _httpClientFactory.CreateClient();

        var url = $"https://graph.facebook.com/v18.0/oauth/access_token" +
                  $"?grant_type=fb_exchange_token" +
                  $"&client_id={appId}" +
                  $"&client_secret={appSecret}" +
                  $"&fb_exchange_token={currentToken}";

        var response = await client.GetAsync(url);
        var content = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            return new OAuthTokenResult
            {
                Success = false,
                Error = "refresh_error",
                ErrorDescription = content
            };
        }

        var tokenResponse = JsonSerializer.Deserialize<FacebookTokenResponse>(content);

        return new OAuthTokenResult
        {
            Success = true,
            AccessToken = tokenResponse?.AccessToken,
            ExpiresAt = tokenResponse?.ExpiresIn > 0
                ? DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn)
                : DateTime.UtcNow.AddDays(60)
        };
    }

    private async Task<OAuthUserInfo?> GetInstagramUserInfoAsync(string accessToken)
    {
        using var client = _httpClientFactory.CreateClient();

        // Primero obtenemos las cuentas de Instagram Business conectadas
        var url = $"https://graph.facebook.com/v18.0/me/accounts?access_token={accessToken}";
        var response = await client.GetAsync(url);

        if (!response.IsSuccessStatusCode) return null;

        var pages = await response.Content.ReadFromJsonAsync<FacebookPagesResponse>();
        if (pages?.Data == null || !pages.Data.Any()) return null;

        // Obtenemos la cuenta de Instagram asociada a la primera página
        var pageId = pages.Data.First().Id;
        var igUrl = $"https://graph.facebook.com/v18.0/{pageId}?fields=instagram_business_account&access_token={accessToken}";
        var igResponse = await client.GetAsync(igUrl);

        if (!igResponse.IsSuccessStatusCode) return null;

        var igData = await igResponse.Content.ReadFromJsonAsync<InstagramAccountResponse>();
        if (igData?.InstagramBusinessAccount == null) return null;

        // Obtenemos info del usuario de Instagram
        var igInfoUrl = $"https://graph.facebook.com/v18.0/{igData.InstagramBusinessAccount.Id}?fields=id,username,name&access_token={accessToken}";
        var igInfoResponse = await client.GetAsync(igInfoUrl);

        if (!igInfoResponse.IsSuccessStatusCode) return null;

        var igUser = await igInfoResponse.Content.ReadFromJsonAsync<InstagramUserResponse>();
        return new OAuthUserInfo
        {
            Id = igUser?.Id,
            Username = igUser?.Username,
            DisplayName = igUser?.Name
        };
    }

    #endregion

    #region X (Twitter)

    private string GetXAuthUrl(string state, string redirectUri, string? codeChallenge)
    {
        var clientId = _configuration["OAuth:X:ClientId"];
        var scopes = DefaultScopes[NetworkType.X];

        if (string.IsNullOrEmpty(codeChallenge))
            throw new ArgumentException("X (Twitter) requiere PKCE. Proporciona codeChallenge.", nameof(codeChallenge));

        // X usa OAuth 2.0 con PKCE obligatorio
        return $"https://twitter.com/i/oauth2/authorize" +
               $"?client_id={clientId}" +
               $"&redirect_uri={HttpUtility.UrlEncode(redirectUri)}" +
               $"&scope={HttpUtility.UrlEncode(scopes)}" +
               $"&state={HttpUtility.UrlEncode(state)}" +
               $"&response_type=code" +
               $"&code_challenge={HttpUtility.UrlEncode(codeChallenge)}" +
               $"&code_challenge_method=S256";
    }

    private async Task<OAuthTokenResult> ExchangeXCodeAsync(string code, string redirectUri, string? codeVerifier)
    {
        if (string.IsNullOrEmpty(codeVerifier))
        {
            return new OAuthTokenResult
            {
                Success = false,
                Error = "pkce_required",
                ErrorDescription = "X (Twitter) requiere code_verifier para PKCE"
            };
        }

        var clientId = _configuration["OAuth:X:ClientId"];
        var clientSecret = _configuration["OAuth:X:ClientSecret"];

        using var client = _httpClientFactory.CreateClient();

        // X requiere Basic Auth con client_id:client_secret
        var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{clientId}:{clientSecret}"));
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", credentials);

        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["redirect_uri"] = redirectUri,
            ["code_verifier"] = codeVerifier
        });

        var response = await client.PostAsync("https://api.twitter.com/2/oauth2/token", content);
        var responseContent = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Error en X OAuth: {Content}", responseContent);
            var errorResponse = JsonSerializer.Deserialize<XErrorResponse>(responseContent);
            return new OAuthTokenResult
            {
                Success = false,
                Error = errorResponse?.Error ?? "x_error",
                ErrorDescription = errorResponse?.ErrorDescription ?? responseContent
            };
        }

        var tokenResponse = JsonSerializer.Deserialize<XTokenResponse>(responseContent);

        return new OAuthTokenResult
        {
            Success = true,
            AccessToken = tokenResponse?.AccessToken,
            RefreshToken = tokenResponse?.RefreshToken,
            ExpiresAt = tokenResponse?.ExpiresIn > 0
                ? DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn)
                : DateTime.UtcNow.AddHours(2), // X tokens duran 2 horas por defecto
            Scopes = tokenResponse?.Scope ?? DefaultScopes[NetworkType.X]
        };
    }

    private async Task<OAuthTokenResult> RefreshXTokenAsync(string refreshToken)
    {
        var clientId = _configuration["OAuth:X:ClientId"];
        var clientSecret = _configuration["OAuth:X:ClientSecret"];

        using var client = _httpClientFactory.CreateClient();

        var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{clientId}:{clientSecret}"));
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", credentials);

        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = refreshToken
        });

        var response = await client.PostAsync("https://api.twitter.com/2/oauth2/token", content);
        var responseContent = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Error refrescando token X: {Content}", responseContent);
            var errorResponse = JsonSerializer.Deserialize<XErrorResponse>(responseContent);
            return new OAuthTokenResult
            {
                Success = false,
                Error = errorResponse?.Error ?? "refresh_error",
                ErrorDescription = errorResponse?.ErrorDescription ?? responseContent
            };
        }

        var tokenResponse = JsonSerializer.Deserialize<XTokenResponse>(responseContent);

        // IMPORTANTE: X puede devolver un nuevo refresh_token (rotación)
        return new OAuthTokenResult
        {
            Success = true,
            AccessToken = tokenResponse?.AccessToken,
            RefreshToken = tokenResponse?.RefreshToken, // Nuevo refresh token si hubo rotación
            ExpiresAt = tokenResponse?.ExpiresIn > 0
                ? DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn)
                : DateTime.UtcNow.AddHours(2)
        };
    }

    private async Task<OAuthUserInfo?> GetXUserInfoAsync(string accessToken)
    {
        using var client = _httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

        var response = await client.GetAsync("https://api.twitter.com/2/users/me?user.fields=id,username,name,profile_image_url");
        if (!response.IsSuccessStatusCode) return null;

        var result = await response.Content.ReadFromJsonAsync<XUserResponse>();
        return new OAuthUserInfo
        {
            Id = result?.Data?.Id,
            Username = result?.Data?.Username,
            DisplayName = result?.Data?.Name,
            ProfileImageUrl = result?.Data?.ProfileImageUrl
        };
    }

    private async Task<bool> RevokeXTokenAsync(string accessToken)
    {
        var clientId = _configuration["OAuth:X:ClientId"];
        var clientSecret = _configuration["OAuth:X:ClientSecret"];

        using var client = _httpClientFactory.CreateClient();

        var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{clientId}:{clientSecret}"));
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", credentials);

        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["token"] = accessToken,
            ["token_type_hint"] = "access_token"
        });

        var response = await client.PostAsync("https://api.twitter.com/2/oauth2/revoke", content);
        return response.IsSuccessStatusCode;
    }

    #endregion

    #region LinkedIn

    private string GetLinkedInAuthUrl(string state, string redirectUri)
    {
        var clientId = _configuration["OAuth:LinkedIn:ClientId"];
        var scopes = DefaultScopes[NetworkType.LinkedIn];

        // LinkedIn OAuth 2.0 (OpenID Connect)
        return $"https://www.linkedin.com/oauth/v2/authorization" +
               $"?client_id={clientId}" +
               $"&redirect_uri={HttpUtility.UrlEncode(redirectUri)}" +
               $"&scope={HttpUtility.UrlEncode(scopes)}" +
               $"&state={HttpUtility.UrlEncode(state)}" +
               $"&response_type=code";
    }

    private async Task<OAuthTokenResult> ExchangeLinkedInCodeAsync(string code, string redirectUri)
    {
        var clientId = _configuration["OAuth:LinkedIn:ClientId"];
        var clientSecret = _configuration["OAuth:LinkedIn:ClientSecret"];

        using var client = _httpClientFactory.CreateClient();

        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["client_id"] = clientId!,
            ["client_secret"] = clientSecret!,
            ["redirect_uri"] = redirectUri
        });

        var response = await client.PostAsync("https://www.linkedin.com/oauth/v2/accessToken", content);
        var responseContent = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Error en LinkedIn OAuth: {Content}", responseContent);
            var errorResponse = JsonSerializer.Deserialize<LinkedInErrorResponse>(responseContent);
            return new OAuthTokenResult
            {
                Success = false,
                Error = errorResponse?.Error ?? "linkedin_error",
                ErrorDescription = errorResponse?.ErrorDescription ?? responseContent
            };
        }

        var tokenResponse = JsonSerializer.Deserialize<LinkedInTokenResponse>(responseContent);

        return new OAuthTokenResult
        {
            Success = true,
            AccessToken = tokenResponse?.AccessToken,
            RefreshToken = tokenResponse?.RefreshToken,
            ExpiresAt = tokenResponse?.ExpiresIn > 0
                ? DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn)
                : DateTime.UtcNow.AddDays(60), // LinkedIn tokens duran ~60 días
            Scopes = DefaultScopes[NetworkType.LinkedIn]
        };
    }

    private async Task<OAuthTokenResult> RefreshLinkedInTokenAsync(string refreshToken)
    {
        var clientId = _configuration["OAuth:LinkedIn:ClientId"];
        var clientSecret = _configuration["OAuth:LinkedIn:ClientSecret"];

        using var client = _httpClientFactory.CreateClient();

        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = refreshToken,
            ["client_id"] = clientId!,
            ["client_secret"] = clientSecret!
        });

        var response = await client.PostAsync("https://www.linkedin.com/oauth/v2/accessToken", content);
        var responseContent = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Error refrescando token LinkedIn: {Content}", responseContent);
            var errorResponse = JsonSerializer.Deserialize<LinkedInErrorResponse>(responseContent);
            return new OAuthTokenResult
            {
                Success = false,
                Error = errorResponse?.Error ?? "refresh_error",
                ErrorDescription = errorResponse?.ErrorDescription ?? responseContent
            };
        }

        var tokenResponse = JsonSerializer.Deserialize<LinkedInTokenResponse>(responseContent);

        return new OAuthTokenResult
        {
            Success = true,
            AccessToken = tokenResponse?.AccessToken,
            RefreshToken = tokenResponse?.RefreshToken ?? refreshToken,
            ExpiresAt = tokenResponse?.ExpiresIn > 0
                ? DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn)
                : DateTime.UtcNow.AddDays(60)
        };
    }

    private async Task<OAuthUserInfo?> GetLinkedInUserInfoAsync(string accessToken)
    {
        using var client = _httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

        // LinkedIn API v2 - userinfo endpoint (OpenID Connect)
        var response = await client.GetAsync("https://api.linkedin.com/v2/userinfo");
        if (!response.IsSuccessStatusCode) return null;

        var user = await response.Content.ReadFromJsonAsync<LinkedInUserInfoResponse>();
        return new OAuthUserInfo
        {
            Id = user?.Sub,
            DisplayName = user?.Name,
            Username = user?.Email ?? user?.Name,
            ProfileImageUrl = user?.Picture
        };
    }

    #endregion

    #region TikTok

    private string GetTikTokAuthUrl(string state, string redirectUri, string? codeChallenge)
    {
        var clientKey = _configuration["OAuth:TikTok:ClientKey"];
        var scopes = DefaultScopes[NetworkType.TikTok];

        if (string.IsNullOrEmpty(codeChallenge))
            throw new ArgumentException("TikTok requiere PKCE. Proporciona codeChallenge.", nameof(codeChallenge));

        // TikTok Login Kit v2 con PKCE
        return $"https://www.tiktok.com/v2/auth/authorize/" +
               $"?client_key={clientKey}" +
               $"&redirect_uri={HttpUtility.UrlEncode(redirectUri)}" +
               $"&scope={HttpUtility.UrlEncode(scopes)}" +
               $"&state={HttpUtility.UrlEncode(state)}" +
               $"&response_type=code" +
               $"&code_challenge={HttpUtility.UrlEncode(codeChallenge)}" +
               $"&code_challenge_method=S256";
    }

    private async Task<OAuthTokenResult> ExchangeTikTokCodeAsync(string code, string redirectUri, string? codeVerifier)
    {
        if (string.IsNullOrEmpty(codeVerifier))
        {
            return new OAuthTokenResult
            {
                Success = false,
                Error = "pkce_required",
                ErrorDescription = "TikTok requiere code_verifier para PKCE"
            };
        }

        var clientKey = _configuration["OAuth:TikTok:ClientKey"];
        var clientSecret = _configuration["OAuth:TikTok:ClientSecret"];

        using var client = _httpClientFactory.CreateClient();

        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_key"] = clientKey!,
            ["client_secret"] = clientSecret!,
            ["code"] = code,
            ["grant_type"] = "authorization_code",
            ["redirect_uri"] = redirectUri,
            ["code_verifier"] = codeVerifier
        });

        var response = await client.PostAsync("https://open.tiktokapis.com/v2/oauth/token/", content);
        var responseContent = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Error en TikTok OAuth: {Content}", responseContent);
            var errorResponse = JsonSerializer.Deserialize<TikTokErrorResponse>(responseContent);
            return new OAuthTokenResult
            {
                Success = false,
                Error = errorResponse?.Error ?? "tiktok_error",
                ErrorDescription = errorResponse?.ErrorDescription ?? responseContent
            };
        }

        var tokenResponse = JsonSerializer.Deserialize<TikTokTokenResponse>(responseContent);

        return new OAuthTokenResult
        {
            Success = true,
            AccessToken = tokenResponse?.AccessToken,
            RefreshToken = tokenResponse?.RefreshToken,
            ExpiresAt = tokenResponse?.ExpiresIn > 0
                ? DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn)
                : DateTime.UtcNow.AddDays(1), // TikTok access tokens duran 24h
            RefreshTokenExpiresAt = tokenResponse?.RefreshExpiresIn > 0
                ? DateTime.UtcNow.AddSeconds(tokenResponse.RefreshExpiresIn)
                : DateTime.UtcNow.AddDays(365), // Refresh tokens duran ~1 año
            Scopes = tokenResponse?.Scope ?? DefaultScopes[NetworkType.TikTok]
        };
    }

    private async Task<OAuthTokenResult> RefreshTikTokTokenAsync(string refreshToken)
    {
        var clientKey = _configuration["OAuth:TikTok:ClientKey"];
        var clientSecret = _configuration["OAuth:TikTok:ClientSecret"];

        using var client = _httpClientFactory.CreateClient();

        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_key"] = clientKey!,
            ["client_secret"] = clientSecret!,
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = refreshToken
        });

        var response = await client.PostAsync("https://open.tiktokapis.com/v2/oauth/token/", content);
        var responseContent = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Error refrescando token TikTok: {Content}", responseContent);
            var errorResponse = JsonSerializer.Deserialize<TikTokErrorResponse>(responseContent);
            return new OAuthTokenResult
            {
                Success = false,
                Error = errorResponse?.Error ?? "refresh_error",
                ErrorDescription = errorResponse?.ErrorDescription ?? responseContent
            };
        }

        var tokenResponse = JsonSerializer.Deserialize<TikTokTokenResponse>(responseContent);

        return new OAuthTokenResult
        {
            Success = true,
            AccessToken = tokenResponse?.AccessToken,
            RefreshToken = tokenResponse?.RefreshToken ?? refreshToken,
            ExpiresAt = tokenResponse?.ExpiresIn > 0
                ? DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn)
                : DateTime.UtcNow.AddDays(1),
            RefreshTokenExpiresAt = tokenResponse?.RefreshExpiresIn > 0
                ? DateTime.UtcNow.AddSeconds(tokenResponse.RefreshExpiresIn)
                : null
        };
    }

    private async Task<OAuthUserInfo?> GetTikTokUserInfoAsync(string accessToken)
    {
        using var client = _httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

        // TikTok API v2 - user info
        var response = await client.GetAsync("https://open.tiktokapis.com/v2/user/info/?fields=open_id,display_name,avatar_url,username");
        if (!response.IsSuccessStatusCode) return null;

        var result = await response.Content.ReadFromJsonAsync<TikTokUserResponse>();
        return new OAuthUserInfo
        {
            Id = result?.Data?.User?.OpenId,
            DisplayName = result?.Data?.User?.DisplayName,
            Username = result?.Data?.User?.Username,
            ProfileImageUrl = result?.Data?.User?.AvatarUrl
        };
    }

    private async Task<bool> RevokeTikTokTokenAsync(string accessToken)
    {
        var clientKey = _configuration["OAuth:TikTok:ClientKey"];
        var clientSecret = _configuration["OAuth:TikTok:ClientSecret"];

        using var client = _httpClientFactory.CreateClient();

        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_key"] = clientKey!,
            ["client_secret"] = clientSecret!,
            ["token"] = accessToken
        });

        var response = await client.PostAsync("https://open.tiktokapis.com/v2/oauth/revoke/", content);
        return response.IsSuccessStatusCode;
    }

    #endregion

    #region YouTube (Google)

    private string GetYouTubeAuthUrl(string state, string redirectUri)
    {
        var clientId = _configuration["OAuth:YouTube:ClientId"];
        var scopes = DefaultScopes[NetworkType.YouTube];

        // Google OAuth 2.0
        return $"https://accounts.google.com/o/oauth2/v2/auth" +
               $"?client_id={clientId}" +
               $"&redirect_uri={HttpUtility.UrlEncode(redirectUri)}" +
               $"&scope={HttpUtility.UrlEncode(scopes)}" +
               $"&state={HttpUtility.UrlEncode(state)}" +
               $"&response_type=code" +
               $"&access_type=offline" +
               $"&prompt=consent"; // Forzar consent para obtener refresh_token
    }

    private async Task<OAuthTokenResult> ExchangeYouTubeCodeAsync(string code, string redirectUri)
    {
        var clientId = _configuration["OAuth:YouTube:ClientId"];
        var clientSecret = _configuration["OAuth:YouTube:ClientSecret"];

        using var client = _httpClientFactory.CreateClient();

        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["client_id"] = clientId!,
            ["client_secret"] = clientSecret!,
            ["redirect_uri"] = redirectUri
        });

        var response = await client.PostAsync("https://oauth2.googleapis.com/token", content);
        var responseContent = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Error en YouTube/Google OAuth: {Content}", responseContent);
            var errorResponse = JsonSerializer.Deserialize<GoogleErrorResponse>(responseContent);
            return new OAuthTokenResult
            {
                Success = false,
                Error = errorResponse?.Error ?? "youtube_error",
                ErrorDescription = errorResponse?.ErrorDescription ?? responseContent
            };
        }

        var tokenResponse = JsonSerializer.Deserialize<GoogleTokenResponse>(responseContent);

        return new OAuthTokenResult
        {
            Success = true,
            AccessToken = tokenResponse?.AccessToken,
            RefreshToken = tokenResponse?.RefreshToken,
            ExpiresAt = tokenResponse?.ExpiresIn > 0
                ? DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn)
                : DateTime.UtcNow.AddHours(1), // Google tokens duran ~1 hora
            Scopes = tokenResponse?.Scope ?? DefaultScopes[NetworkType.YouTube]
        };
    }

    private async Task<OAuthTokenResult> RefreshYouTubeTokenAsync(string refreshToken)
    {
        var clientId = _configuration["OAuth:YouTube:ClientId"];
        var clientSecret = _configuration["OAuth:YouTube:ClientSecret"];

        using var client = _httpClientFactory.CreateClient();

        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = refreshToken,
            ["client_id"] = clientId!,
            ["client_secret"] = clientSecret!
        });

        var response = await client.PostAsync("https://oauth2.googleapis.com/token", content);
        var responseContent = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Error refrescando token YouTube/Google: {Content}", responseContent);
            var errorResponse = JsonSerializer.Deserialize<GoogleErrorResponse>(responseContent);
            return new OAuthTokenResult
            {
                Success = false,
                Error = errorResponse?.Error ?? "refresh_error",
                ErrorDescription = errorResponse?.ErrorDescription ?? responseContent
            };
        }

        var tokenResponse = JsonSerializer.Deserialize<GoogleTokenResponse>(responseContent);

        return new OAuthTokenResult
        {
            Success = true,
            AccessToken = tokenResponse?.AccessToken,
            RefreshToken = refreshToken, // Google no devuelve nuevo refresh_token en refresh
            ExpiresAt = tokenResponse?.ExpiresIn > 0
                ? DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn)
                : DateTime.UtcNow.AddHours(1)
        };
    }

    private async Task<OAuthUserInfo?> GetYouTubeUserInfoAsync(string accessToken)
    {
        using var client = _httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

        // Primero obtenemos info del usuario de Google
        var userInfoResponse = await client.GetAsync("https://www.googleapis.com/oauth2/v2/userinfo");
        if (!userInfoResponse.IsSuccessStatusCode) return null;

        var userInfo = await userInfoResponse.Content.ReadFromJsonAsync<GoogleUserInfoResponse>();

        // Luego obtenemos el canal de YouTube
        var channelResponse = await client.GetAsync("https://www.googleapis.com/youtube/v3/channels?part=snippet&mine=true");
        string? channelId = null;
        string? channelTitle = null;

        if (channelResponse.IsSuccessStatusCode)
        {
            var channelData = await channelResponse.Content.ReadFromJsonAsync<YouTubeChannelResponse>();
            var channel = channelData?.Items?.FirstOrDefault();
            channelId = channel?.Id;
            channelTitle = channel?.Snippet?.Title;
        }

        return new OAuthUserInfo
        {
            Id = channelId ?? userInfo?.Id,
            DisplayName = channelTitle ?? userInfo?.Name,
            Username = userInfo?.Email,
            ProfileImageUrl = userInfo?.Picture
        };
    }

    private async Task<bool> RevokeYouTubeTokenAsync(string accessToken)
    {
        using var client = _httpClientFactory.CreateClient();

        var response = await client.PostAsync(
            $"https://oauth2.googleapis.com/revoke?token={HttpUtility.UrlEncode(accessToken)}",
            new StringContent(string.Empty));

        return response.IsSuccessStatusCode;
    }

    #endregion

    #region Response Models

    private class FacebookTokenResponse
    {
        [JsonPropertyName("access_token")]
        public string? AccessToken { get; set; }

        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; set; }

        [JsonPropertyName("token_type")]
        public string? TokenType { get; set; }
    }

    private class FacebookErrorResponse
    {
        [JsonPropertyName("error")]
        public FacebookError? Error { get; set; }
    }

    private class FacebookError
    {
        [JsonPropertyName("message")]
        public string? Message { get; set; }

        [JsonPropertyName("code")]
        public int? Code { get; set; }
    }

    private class FacebookUserResponse
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }
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

    private class InstagramAccountResponse
    {
        [JsonPropertyName("instagram_business_account")]
        public InstagramBusinessAccount? InstagramBusinessAccount { get; set; }
    }

    private class InstagramBusinessAccount
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }
    }

    private class InstagramUserResponse
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("username")]
        public string? Username { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }
    }

    private class XTokenResponse
    {
        [JsonPropertyName("access_token")]
        public string? AccessToken { get; set; }

        [JsonPropertyName("refresh_token")]
        public string? RefreshToken { get; set; }

        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; set; }

        [JsonPropertyName("scope")]
        public string? Scope { get; set; }

        [JsonPropertyName("token_type")]
        public string? TokenType { get; set; }
    }

    private class XErrorResponse
    {
        [JsonPropertyName("error")]
        public string? Error { get; set; }

        [JsonPropertyName("error_description")]
        public string? ErrorDescription { get; set; }
    }

    private class XUserResponse
    {
        [JsonPropertyName("data")]
        public XUserData? Data { get; set; }
    }

    private class XUserData
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("username")]
        public string? Username { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("profile_image_url")]
        public string? ProfileImageUrl { get; set; }
    }

    // LinkedIn Response Models
    private class LinkedInTokenResponse
    {
        [JsonPropertyName("access_token")]
        public string? AccessToken { get; set; }

        [JsonPropertyName("refresh_token")]
        public string? RefreshToken { get; set; }

        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; set; }

        [JsonPropertyName("refresh_token_expires_in")]
        public int RefreshTokenExpiresIn { get; set; }

        [JsonPropertyName("scope")]
        public string? Scope { get; set; }

        [JsonPropertyName("token_type")]
        public string? TokenType { get; set; }
    }

    private class LinkedInErrorResponse
    {
        [JsonPropertyName("error")]
        public string? Error { get; set; }

        [JsonPropertyName("error_description")]
        public string? ErrorDescription { get; set; }
    }

    private class LinkedInUserInfoResponse
    {
        [JsonPropertyName("sub")]
        public string? Sub { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("given_name")]
        public string? GivenName { get; set; }

        [JsonPropertyName("family_name")]
        public string? FamilyName { get; set; }

        [JsonPropertyName("picture")]
        public string? Picture { get; set; }

        [JsonPropertyName("email")]
        public string? Email { get; set; }

        [JsonPropertyName("email_verified")]
        public bool? EmailVerified { get; set; }

        [JsonPropertyName("locale")]
        public string? Locale { get; set; }
    }

    // TikTok Response Models
    private class TikTokTokenResponse
    {
        [JsonPropertyName("access_token")]
        public string? AccessToken { get; set; }

        [JsonPropertyName("refresh_token")]
        public string? RefreshToken { get; set; }

        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; set; }

        [JsonPropertyName("refresh_expires_in")]
        public int RefreshExpiresIn { get; set; }

        [JsonPropertyName("scope")]
        public string? Scope { get; set; }

        [JsonPropertyName("token_type")]
        public string? TokenType { get; set; }

        [JsonPropertyName("open_id")]
        public string? OpenId { get; set; }
    }

    private class TikTokErrorResponse
    {
        [JsonPropertyName("error")]
        public string? Error { get; set; }

        [JsonPropertyName("error_description")]
        public string? ErrorDescription { get; set; }
    }

    private class TikTokUserResponse
    {
        [JsonPropertyName("data")]
        public TikTokUserData? Data { get; set; }
    }

    private class TikTokUserData
    {
        [JsonPropertyName("user")]
        public TikTokUser? User { get; set; }
    }

    private class TikTokUser
    {
        [JsonPropertyName("open_id")]
        public string? OpenId { get; set; }

        [JsonPropertyName("display_name")]
        public string? DisplayName { get; set; }

        [JsonPropertyName("avatar_url")]
        public string? AvatarUrl { get; set; }

        [JsonPropertyName("username")]
        public string? Username { get; set; }
    }

    // YouTube/Google Response Models
    private class GoogleTokenResponse
    {
        [JsonPropertyName("access_token")]
        public string? AccessToken { get; set; }

        [JsonPropertyName("refresh_token")]
        public string? RefreshToken { get; set; }

        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; set; }

        [JsonPropertyName("scope")]
        public string? Scope { get; set; }

        [JsonPropertyName("token_type")]
        public string? TokenType { get; set; }
    }

    private class GoogleErrorResponse
    {
        [JsonPropertyName("error")]
        public string? Error { get; set; }

        [JsonPropertyName("error_description")]
        public string? ErrorDescription { get; set; }
    }

    private class GoogleUserInfoResponse
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("email")]
        public string? Email { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("given_name")]
        public string? GivenName { get; set; }

        [JsonPropertyName("family_name")]
        public string? FamilyName { get; set; }

        [JsonPropertyName("picture")]
        public string? Picture { get; set; }
    }

    private class YouTubeChannelResponse
    {
        [JsonPropertyName("items")]
        public List<YouTubeChannel>? Items { get; set; }
    }

    private class YouTubeChannel
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("snippet")]
        public YouTubeChannelSnippet? Snippet { get; set; }
    }

    private class YouTubeChannelSnippet
    {
        [JsonPropertyName("title")]
        public string? Title { get; set; }

        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("customUrl")]
        public string? CustomUrl { get; set; }

        [JsonPropertyName("thumbnails")]
        public YouTubeThumbnails? Thumbnails { get; set; }
    }

    private class YouTubeThumbnails
    {
        [JsonPropertyName("default")]
        public YouTubeThumbnail? Default { get; set; }
    }

    private class YouTubeThumbnail
    {
        [JsonPropertyName("url")]
        public string? Url { get; set; }
    }

    #endregion
}
