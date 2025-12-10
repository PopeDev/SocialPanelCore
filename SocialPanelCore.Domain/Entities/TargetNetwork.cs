using SocialPanelCore.Domain.Enums;

namespace SocialPanelCore.Domain.Entities;

public class TargetNetwork
{
    public Guid Id { get; set; }
    public Guid BasePostId { get; set; }
    public NetworkType NetworkType { get; set; }

    public BasePost? BasePost { get; set; }
}
