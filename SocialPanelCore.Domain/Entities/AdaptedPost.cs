using SocialPanelCore.Domain.Enums;

namespace SocialPanelCore.Domain.Entities;

public class AdaptedPost
{
    public Guid Id { get; set; }
    public Guid BasePostId { get; set; }
    public NetworkType NetworkType { get; set; }
    public string AdaptedContent { get; set; } = string.Empty;
    public int CharacterCount { get; set; }
    public AdaptedPostState State { get; set; }
    public DateTime? PublishedAt { get; set; }
    public string? ExternalPostId { get; set; }
    public string? LastError { get; set; }
    public int RetryCount { get; set; }
    public DateTime CreatedAt { get; set; }

    // Navegación
    public virtual BasePost BasePost { get; set; } = null!;
}
