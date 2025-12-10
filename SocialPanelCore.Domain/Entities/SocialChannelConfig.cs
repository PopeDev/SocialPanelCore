using SocialPanelCore.Domain.Enums;

namespace SocialPanelCore.Domain.Entities;

public class SocialChannelConfig
{
    public Guid Id { get; set; }
    public Guid AccountId { get; set; }
    public NetworkType NetworkType { get; set; }
    public string? Handle { get; set; }
    public string? AccessToken { get; set; }
    public string? RefreshToken { get; set; }
    public DateTime? TokenExpiresAt { get; set; }
    public bool IsEnabled { get; set; }
    public HealthStatus HealthStatus { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public Account? Account { get; set; }
}
