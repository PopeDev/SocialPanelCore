namespace SocialPanelCore.Domain.Entities;

/// <summary>
/// Proyecto para agrupar gastos y recordatorios.
/// </summary>
public class Project
{
    public Guid Id { get; set; }

    /// <summary>
    /// Cuenta asociada al proyecto.
    /// </summary>
    public Guid AccountId { get; set; }

    /// <summary>
    /// Nombre del proyecto.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Descripcion del proyecto.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Presupuesto total del proyecto.
    /// </summary>
    public decimal? Budget { get; set; }

    /// <summary>
    /// Fecha de inicio del proyecto.
    /// </summary>
    public DateTime? StartDate { get; set; }

    /// <summary>
    /// Fecha de fin del proyecto.
    /// </summary>
    public DateTime? EndDate { get; set; }

    /// <summary>
    /// Indica si el proyecto esta activo.
    /// </summary>
    public bool IsActive { get; set; } = true;

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
    public virtual ICollection<Expense> Expenses { get; set; } = new List<Expense>();
    public virtual ICollection<Reminder> Reminders { get; set; } = new List<Reminder>();
}
