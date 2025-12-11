using SocialPanelCore.Domain.Enums;

namespace SocialPanelCore.Domain.Entities;

public class PostTargetNetwork
{
    public Guid Id { get; set; }
    public Guid BasePostId { get; set; }
    public NetworkType NetworkType { get; set; }

    // Navegación
    public virtual BasePost BasePost { get; set; } = null!;
}
