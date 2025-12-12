using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Web;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Logging;
using SocialPanelCore.Domain.Enums;
using SocialPanelCore.Domain.Interfaces;

namespace SocialPanelCore.Infrastructure.Services;

/// <summary>
/// Servicio para manejar flujos OAuth con redes sociales.
/// </summary>
public class OAuthService : IOAuthService
{
    private readonly IConfiguration _configuration;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<OAuthService> _logger;

    public OAuthService(
        IConfiguration configuration,
        IHttpClientFactory httpClientFactory,
        ILogger<OAuthService> logger)
    {
        _configuration = configuration;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public string GetAuthorizationUrl(NetworkType networkType, Guid accountId, string redirectUri)
    {
        var state = $"{accountId}|{networkType}|{Guid.NewGuid()}";
        var encodedState = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(state));

        return networkType switch
        {
            NetworkType.Facebook => GetFacebookAuthUrl(redirectUri, encodedState),
            NetworkType.Instagram => GetInstagramAuthUrl(redirectUri, encodedState),
            NetworkType.LinkedIn => GetLinkedInAuthUrl(redirectUri, encodedState),
            NetworkType.YouTube => GetYouTubeAuthUrl(redirectUri, encodedState),
            NetworkType.TikTok => GetTikTokAuthUrl(redirectUri, encodedState),
            _ => throw new NotSupportedException($"OAuth no soportado para {networkType}")
        };
    }

    public async Task<OAuthTokenResult> ExchangeCodeForTokensAsync(NetworkType networkType, string code, string redirectUri)
    {
        try
        {
            return networkType switch
            {
                NetworkType.Facebook => await ExchangeFacebookCodeAsync(code, redirectUri),
                NetworkType.Instagram => await ExchangeInstagramCodeAsync(code, redirectUri),
                NetworkType.LinkedIn => await ExchangeLinkedInCodeAsync(code, redirectUri),
                NetworkType.YouTube => await ExchangeYouTubeCodeAsync(code, redirectUri),
                NetworkType.TikTok => await ExchangeTikTokCodeAsync(code, redirectUri),
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
                NetworkType.LinkedIn => await GetLinkedInUserInfoAsync(accessToken),
                _ => null
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error obteniendo info de usuario para {NetworkType}", networkType);
            return null;
        }
    }

    #region Facebook

    private string GetFacebookAuthUrl(string redirectUri, string state)
    {
        var appId = _configuration["OAuth:Facebook:AppId"];
        var scopes = "pages_manage_posts,pages_read_engagement,pages_show_list,public_profile";

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

        var tokenResponse = JsonSerializer.Deserialize<FacebookTokenResponse>(content);

        return new OAuthTokenResult
        {
            Success = true,
            AccessToken = tokenResponse?.AccessToken,
            ExpiresAt = tokenResponse?.ExpiresIn > 0
                ? DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn)
                : null
        };
    }

    private async Task<OAuthTokenResult> RefreshFacebookTokenAsync(string currentToken)
    {
        var appId = _configuration["OAuth:Facebook:AppId"];
        var appSecret = _configuration["OAuth:Facebook:AppSecret"];

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
            return new OAuthTokenResult { Success = false, Error = "refresh_error", ErrorDescription = content };
        }

        var tokenResponse = JsonSerializer.Deserialize<FacebookTokenResponse>(content);

        return new OAuthTokenResult
        {
            Success = true,
            AccessToken = tokenResponse?.AccessToken,
            ExpiresAt = tokenResponse?.ExpiresIn > 0
                ? DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn)
                : null
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

    private string GetInstagramAuthUrl(string redirectUri, string state)
    {
        var appId = _configuration["OAuth:Instagram:AppId"];
        var scopes = "instagram_business_basic,instagram_business_manage_messages,instagram_business_manage_comments,instagram_business_content_publish";

        return $"https://www.facebook.com/v18.0/dialog/oauth" +
               $"?client_id={appId}" +
               $"&redirect_uri={HttpUtility.UrlEncode(redirectUri)}" +
               $"&scope={HttpUtility.UrlEncode(scopes)}" +
               $"&state={HttpUtility.UrlEncode(state)}" +
               $"&response_type=code";
    }

    private async Task<OAuthTokenResult> ExchangeInstagramCodeAsync(string code, string redirectUri)
    {
        // Instagram usa la misma API que Facebook (Meta Graph API)
        return await ExchangeFacebookCodeAsync(code, redirectUri);
    }

    private async Task<OAuthTokenResult> RefreshInstagramTokenAsync(string refreshToken)
    {
        return await RefreshFacebookTokenAsync(refreshToken);
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

        // Obtenemos la cuenta de Instagram asociada a la primera pagina
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

    #region LinkedIn

    private string GetLinkedInAuthUrl(string redirectUri, string state)
    {
        var clientId = _configuration["OAuth:LinkedIn:ClientId"];
        var scopes = "w_member_social";

        return $"https://www.linkedin.com/oauth/v2/authorization" +
               $"?response_type=code" +
               $"&client_id={clientId}" +
               $"&redirect_uri={HttpUtility.UrlEncode(redirectUri)}" +
               $"&scope={HttpUtility.UrlEncode(scopes)}" +
               $"&state={HttpUtility.UrlEncode(state)}";
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
            return new OAuthTokenResult { Success = false, Error = "linkedin_error", ErrorDescription = responseContent };
        }

        var tokenResponse = JsonSerializer.Deserialize<LinkedInTokenResponse>(responseContent);

        return new OAuthTokenResult
        {
            Success = true,
            AccessToken = tokenResponse?.AccessToken,
            RefreshToken = tokenResponse?.RefreshToken,
            ExpiresAt = tokenResponse?.ExpiresIn > 0
                ? DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn)
                : null
        };
    }

    private async Task<OAuthUserInfo?> GetLinkedInUserInfoAsync(string accessToken)
    {
        using var client = _httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

        var response = await client.GetAsync("https://api.linkedin.com/v2/userinfo");
        if (!response.IsSuccessStatusCode) return null;

        var user = await response.Content.ReadFromJsonAsync<LinkedInUserResponse>();
        return new OAuthUserInfo
        {
            Id = user?.Sub,
            DisplayName = user?.Name,
            Username = user?.Name
        };
    }

    #endregion

    #region YouTube

    private string GetYouTubeAuthUrl(string redirectUri, string state)
    {
        var clientId = _configuration["OAuth:YouTube:ClientId"];
        var scopes = "https://www.googleapis.com/auth/youtube.upload https://www.googleapis.com/auth/youtube";

        return $"https://accounts.google.com/o/oauth2/v2/auth" +
               $"?client_id={clientId}" +
               $"&redirect_uri={HttpUtility.UrlEncode(redirectUri)}" +
               $"&response_type=code" +
               $"&scope={HttpUtility.UrlEncode(scopes)}" +
               $"&access_type=offline" +
               $"&state={HttpUtility.UrlEncode(state)}";
    }

    private async Task<OAuthTokenResult> ExchangeYouTubeCodeAsync(string code, string redirectUri)
    {
        var clientId = _configuration["OAuth:YouTube:ClientId"];
        var clientSecret = _configuration["OAuth:YouTube:ClientSecret"];

        using var client = _httpClientFactory.CreateClient();
        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["code"] = code,
            ["client_id"] = clientId!,
            ["client_secret"] = clientSecret!,
            ["redirect_uri"] = redirectUri,
            ["grant_type"] = "authorization_code"
        });

        var response = await client.PostAsync("https://oauth2.googleapis.com/token", content);
        var responseContent = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            return new OAuthTokenResult { Success = false, Error = "youtube_error", ErrorDescription = responseContent };
        }

        var tokenResponse = JsonSerializer.Deserialize<GoogleTokenResponse>(responseContent);

        return new OAuthTokenResult
        {
            Success = true,
            AccessToken = tokenResponse?.AccessToken,
            RefreshToken = tokenResponse?.RefreshToken,
            ExpiresAt = tokenResponse?.ExpiresIn > 0
                ? DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn)
                : null
        };
    }

    #endregion

    #region TikTok

    private string GetTikTokAuthUrl(string redirectUri, string state)
    {
        var clientKey = _configuration["OAuth:TikTok:ClientKey"];
        var scopes = "video.upload,video.publish";

        return $"https://www.tiktok.com/v2/auth/authorize/" +
               $"?client_key={clientKey}" +
               $"&redirect_uri={HttpUtility.UrlEncode(redirectUri)}" +
               $"&scope={HttpUtility.UrlEncode(scopes)}" +
               $"&response_type=code" +
               $"&state={HttpUtility.UrlEncode(state)}";
    }

    private async Task<OAuthTokenResult> ExchangeTikTokCodeAsync(string code, string redirectUri)
    {
        var clientKey = _configuration["OAuth:TikTok:ClientKey"];
        var clientSecret = _configuration["OAuth:TikTok:ClientSecret"];

        using var client = _httpClientFactory.CreateClient();
        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_key"] = clientKey!,
            ["client_secret"] = clientSecret!,
            ["code"] = code,
            ["grant_type"] = "authorization_code",
            ["redirect_uri"] = redirectUri
        });

        var response = await client.PostAsync("https://open.tiktokapis.com/v2/oauth/token/", content);
        var responseContent = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            return new OAuthTokenResult { Success = false, Error = "tiktok_error", ErrorDescription = responseContent };
        }

        var tokenResponse = JsonSerializer.Deserialize<TikTokTokenResponse>(responseContent);

        return new OAuthTokenResult
        {
            Success = true,
            AccessToken = tokenResponse?.AccessToken,
            RefreshToken = tokenResponse?.RefreshToken,
            ExpiresAt = tokenResponse?.ExpiresIn > 0
                ? DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn)
                : null
        };
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

    private class LinkedInTokenResponse
    {
        [JsonPropertyName("access_token")]
        public string? AccessToken { get; set; }

        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; set; }

        [JsonPropertyName("refresh_token")]
        public string? RefreshToken { get; set; }
    }

    private class LinkedInUserResponse
    {
        [JsonPropertyName("sub")]
        public string? Sub { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }
    }

    private class GoogleTokenResponse
    {
        [JsonPropertyName("access_token")]
        public string? AccessToken { get; set; }

        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; set; }

        [JsonPropertyName("refresh_token")]
        public string? RefreshToken { get; set; }

        [JsonPropertyName("token_type")]
        public string? TokenType { get; set; }
    }

    private class TikTokTokenResponse
    {
        [JsonPropertyName("access_token")]
        public string? AccessToken { get; set; }

        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; set; }

        [JsonPropertyName("refresh_token")]
        public string? RefreshToken { get; set; }

        [JsonPropertyName("open_id")]
        public string? OpenId { get; set; }
    }

    #endregion
}
