using SocialPanelCore.Domain.Entities;
using SocialPanelCore.Domain.Enums;

namespace SocialPanelCore.Domain.Interfaces;

public interface ISocialChannelConfigService
{
    Task<IEnumerable<SocialChannelConfig>> GetChannelConfigsByAccountAsync(Guid accountId);
    Task<SocialChannelConfig?> GetChannelConfigAsync(Guid id);
    Task<SocialChannelConfig?> GetChannelConfigByAccountAndNetworkAsync(Guid accountId, NetworkType networkType);

    // ========== Creación con OAuth ==========
    Task<SocialChannelConfig> CreateChannelConfigAsync(
        Guid accountId,
        NetworkType networkType,
        string accessToken,
        string? refreshToken,
        DateTime? tokenExpiresAt,
        string? handle);

    // ========== Creación con ApiKey (X/Twitter, Telegram) ==========
    Task<SocialChannelConfig> CreateChannelConfigWithApiKeyAsync(
        Guid accountId,
        NetworkType networkType,
        string apiKey,
        string apiSecret,
        string accessToken,
        string accessTokenSecret,
        string? handle);

    // ========== Actualización ==========
    Task UpdateTokensAsync(Guid id, string accessToken, string? refreshToken, DateTime? tokenExpiresAt);
    Task UpdateApiKeyCredentialsAsync(Guid id, string apiKey, string apiSecret, string accessToken, string accessTokenSecret);

    // ========== Activación/Desactivación ==========
    Task EnableChannelAsync(Guid id);
    Task DisableChannelAsync(Guid id);
    Task UpdateHealthStatusAsync(Guid id, HealthStatus status, string? errorMessage = null);
    Task DeleteChannelAsync(Guid id);

    // ========== Obtención de credenciales (descifradas) ==========
    Task<(string ApiKey, string ApiSecret, string AccessToken, string AccessTokenSecret)?> GetDecryptedApiKeyCredentialsAsync(Guid id);
    Task<(string AccessToken, string? RefreshToken)?> GetDecryptedOAuthCredentialsAsync(Guid id);

    // ========== Verificación de conexión ==========
    Task<bool> TestConnectionAsync(Guid id);
}
