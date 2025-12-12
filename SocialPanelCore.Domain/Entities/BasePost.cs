using SocialPanelCore.Domain.Enums;

namespace SocialPanelCore.Domain.Entities;

public class BasePost
{
    public Guid Id { get; set; }
    public Guid AccountId { get; set; }
    public Guid? CreatedByUserId { get; set; }
    public string? Title { get; set; }
    public string Content { get; set; } = string.Empty;
    public DateTime ScheduledAtUtc { get; set; }
    public BasePostState State { get; set; }
    public ContentType ContentType { get; set; }

    // ========== AI OPTIMIZATION ==========

    /// <summary>
    /// Indica si la publicación debe ser optimizada por IA.
    /// Si es true, el contenido se adaptará automáticamente para cada red.
    /// Si es false, el contenido se publicará tal cual (en bruto).
    /// </summary>
    public bool AiOptimizationEnabled { get; set; } = true;

    /// <summary>
    /// Modo de publicación: Scheduled (programada) o Immediate (inmediata)
    /// </summary>
    public PublishMode PublishMode { get; set; } = PublishMode.Scheduled;

    // ========== FIN AI OPTIMIZATION ==========

    public bool RequiresApproval { get; set; }

    // Aprobación/Rechazo
    public Guid? ApprovedByUserId { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public string? ApprovalNotes { get; set; }
    public Guid? RejectedByUserId { get; set; }
    public DateTime? RejectedAt { get; set; }
    public string? RejectionNotes { get; set; }

    // Publicación
    public DateTime? PublishedAt { get; set; }

    // Auditoría
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Navegación
    public virtual Account Account { get; set; } = null!;
    public virtual User? CreatedByUser { get; set; }
    public virtual ICollection<PostTargetNetwork> TargetNetworks { get; set; } = new List<PostTargetNetwork>();
    public virtual ICollection<AdaptedPost> AdaptedVersions { get; set; } = new List<AdaptedPost>();

    /// <summary>
    /// Archivos multimedia (imágenes) asociados a esta publicación
    /// </summary>
    public virtual ICollection<PostMedia> Media { get; set; } = new List<PostMedia>();
}
