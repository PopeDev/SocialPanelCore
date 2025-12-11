using SocialPanelCore.Domain.Entities;
using SocialPanelCore.Domain.Enums;

namespace SocialPanelCore.Domain.Interfaces;

public interface ISocialChannelConfigService
{
    Task<IEnumerable<SocialChannelConfig>> GetChannelConfigsByAccountAsync(Guid accountId);
    Task<SocialChannelConfig?> GetChannelConfigAsync(Guid id);
    Task<SocialChannelConfig> CreateChannelConfigAsync(
        Guid accountId,
        NetworkType networkType,
        string accessToken,
        string? refreshToken,
        DateTime? tokenExpiresAt,
        string? handle);
    Task UpdateTokensAsync(Guid id, string accessToken, string? refreshToken, DateTime? tokenExpiresAt);
    Task EnableChannelAsync(Guid id);
    Task DisableChannelAsync(Guid id);
    Task UpdateHealthStatusAsync(Guid id, HealthStatus status, string? errorMessage = null);
    Task DeleteChannelAsync(Guid id);
}
