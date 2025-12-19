namespace SocialPanelCore.Domain.Entities;

/// <summary>
/// Gasto registrado en el sistema.
/// </summary>
public class Expense
{
    public Guid Id { get; set; }

    /// <summary>
    /// Cuenta asociada al gasto.
    /// </summary>
    public Guid AccountId { get; set; }

    /// <summary>
    /// Proyecto asociado al gasto (opcional).
    /// </summary>
    public Guid? ProjectId { get; set; }

    /// <summary>
    /// Descripcion del gasto.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Monto del gasto.
    /// </summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// Categoria del gasto.
    /// </summary>
    public string? Category { get; set; }

    /// <summary>
    /// Fecha del gasto.
    /// </summary>
    public DateTime ExpenseDate { get; set; }

    /// <summary>
    /// Notas adicionales.
    /// </summary>
    public string? Notes { get; set; }

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
    public virtual Project? Project { get; set; }
}
