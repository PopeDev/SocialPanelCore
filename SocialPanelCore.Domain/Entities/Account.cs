namespace SocialPanelCore.Domain.Entities;

public class Account
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public ICollection<BasePost> Posts { get; set; } = new List<BasePost>();
    public ICollection<SocialChannelConfig> SocialChannels { get; set; } = new List<SocialChannelConfig>();
}
