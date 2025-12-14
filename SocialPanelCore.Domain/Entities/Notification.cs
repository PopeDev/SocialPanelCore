using SocialPanelCore.Domain.Enums;

namespace SocialPanelCore.Domain.Entities;

/// <summary>
/// Notificación del sistema para usuarios.
/// </summary>
public class Notification
{
    public Guid Id { get; set; }

    /// <summary>
    /// Usuario destinatario de la notificación.
    /// Si es null, es una notificación global para todos los usuarios de la cuenta.
    /// </summary>
    public Guid? UserId { get; set; }

    /// <summary>
    /// Cuenta asociada a la notificación.
    /// </summary>
    public Guid AccountId { get; set; }

    /// <summary>
    /// Tipo de notificación.
    /// </summary>
    public NotificationType Type { get; set; }

    /// <summary>
    /// Título corto de la notificación.
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Mensaje detallado de la notificación.
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Red social relacionada (si aplica).
    /// </summary>
    public NetworkType? RelatedNetwork { get; set; }

    /// <summary>
    /// ID del canal social relacionado (si aplica).
    /// </summary>
    public Guid? RelatedChannelId { get; set; }

    /// <summary>
    /// URL de acción sugerida (ej: reconectar OAuth).
    /// </summary>
    public string? ActionUrl { get; set; }

    /// <summary>
    /// Texto del botón de acción.
    /// </summary>
    public string? ActionText { get; set; }

    /// <summary>
    /// Indica si la notificación ha sido leída.
    /// </summary>
    public bool IsRead { get; set; }

    /// <summary>
    /// Indica si la notificación ha sido descartada.
    /// </summary>
    public bool IsDismissed { get; set; }

    /// <summary>
    /// Fecha de creación (UTC).
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Fecha de lectura (UTC).
    /// </summary>
    public DateTime? ReadAt { get; set; }

    /// <summary>
    /// Fecha de expiración automática (UTC). Después de esta fecha, no se mostrará.
    /// </summary>
    public DateTime? ExpiresAt { get; set; }

    // Navegación
    public virtual User? User { get; set; }
    public virtual Account Account { get; set; } = null!;
    public virtual SocialChannelConfig? RelatedChannel { get; set; }
}
