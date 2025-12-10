using SocialPanelCore.Domain.Enums;

namespace SocialPanelCore.Domain.Entities;

public class BasePost
{
    public Guid Id { get; set; }
    public Guid AccountId { get; set; }
    public Guid? CreatedByUserId { get; set; }
    public string? Title { get; set; }
    public string Content { get; set; } = string.Empty;
    public ContentType ContentType { get; set; }
    public BasePostState State { get; set; }
    public DateTime ScheduledAtUtc { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public Account? Account { get; set; }
    public User? CreatedByUser { get; set; }
    public ICollection<TargetNetwork> TargetNetworks { get; set; } = new List<TargetNetwork>();
}
