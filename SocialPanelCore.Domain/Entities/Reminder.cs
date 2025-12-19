namespace SocialPanelCore.Domain.Entities;

/// <summary>
/// Recordatorio del sistema.
/// </summary>
public class Reminder
{
    public Guid Id { get; set; }

    /// <summary>
    /// Cuenta asociada al recordatorio.
    /// </summary>
    public Guid AccountId { get; set; }

    /// <summary>
    /// Usuario asignado al recordatorio (opcional).
    /// </summary>
    public Guid? UserId { get; set; }

    /// <summary>
    /// Proyecto asociado al recordatorio (opcional).
    /// </summary>
    public Guid? ProjectId { get; set; }

    /// <summary>
    /// Titulo del recordatorio.
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Descripcion del recordatorio.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Fecha y hora del recordatorio.
    /// </summary>
    public DateTime DueDate { get; set; }

    /// <summary>
    /// Indica si el recordatorio ha sido completado.
    /// </summary>
    public bool IsCompleted { get; set; }

    /// <summary>
    /// Fecha en que se completo el recordatorio.
    /// </summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// Fecha de creacion (UTC).
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Fecha de ultima actualizacion (UTC).
    /// </summary>
    public DateTime UpdatedAt { get; set; }

    // Navegacion
    public virtual Account Account { get; set; } = null!;
    public virtual User? User { get; set; }
    public virtual Project? Project { get; set; }
}
