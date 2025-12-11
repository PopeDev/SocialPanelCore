namespace SocialPanelCore.Domain.Entities;

public class UserAccountAccess
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid AccountId { get; set; }
    public bool CanEdit { get; set; }
    public bool CanPublish { get; set; }
    public bool CanApprove { get; set; }
    public DateTime CreatedAt { get; set; }

    // Navegación
    public virtual User User { get; set; } = null!;
    public virtual Account Account { get; set; } = null!;
}
