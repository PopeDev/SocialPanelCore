using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SocialPanelCore.Domain.Entities;
using SocialPanelCore.Domain.Interfaces;
using SocialPanelCore.Infrastructure.Data;

namespace SocialPanelCore.Infrastructure.Services;

public class ExpenseService : IExpenseService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<ExpenseService> _logger;

    public ExpenseService(ApplicationDbContext context, ILogger<ExpenseService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<IEnumerable<Expense>> GetAllExpensesAsync(Guid accountId)
    {
        _logger.LogInformation("Obteniendo gastos de cuenta: {AccountId}", accountId);
        return await _context.Expenses
            .AsNoTracking()
            .Include(e => e.Project)
            .Where(e => e.AccountId == accountId)
            .OrderByDescending(e => e.ExpenseDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<Expense>> GetExpensesByProjectAsync(Guid projectId)
    {
        return await _context.Expenses
            .AsNoTracking()
            .Where(e => e.ProjectId == projectId)
            .OrderByDescending(e => e.ExpenseDate)
            .ToListAsync();
    }

    public async Task<Expense?> GetExpenseByIdAsync(Guid id)
    {
        return await _context.Expenses
            .AsNoTracking()
            .Include(e => e.Project)
            .FirstOrDefaultAsync(e => e.Id == id);
    }

    public async Task<Expense> CreateExpenseAsync(
        Guid accountId,
        Guid? projectId,
        string description,
        decimal amount,
        string? category,
        DateTime expenseDate,
        string? notes)
    {
        if (string.IsNullOrWhiteSpace(description))
            throw new ArgumentException("La descripcion del gasto es obligatoria", nameof(description));

        if (amount <= 0)
            throw new ArgumentException("El monto debe ser mayor a cero", nameof(amount));

        var accountExists = await _context.Accounts.AnyAsync(a => a.Id == accountId);
        if (!accountExists)
            throw new InvalidOperationException($"Cuenta no encontrada: {accountId}");

        // Validar proyecto si se proporciona
        if (projectId.HasValue)
        {
            var projectExists = await _context.Projects.AnyAsync(p => p.Id == projectId.Value);
            if (!projectExists)
                throw new InvalidOperationException($"Proyecto no encontrado: {projectId}");
        }

        var expense = new Expense
        {
            Id = Guid.NewGuid(),
            AccountId = accountId,
            ProjectId = projectId,
            Description = description.Trim(),
            Amount = amount,
            Category = category?.Trim(),
            ExpenseDate = expenseDate,
            Notes = notes?.Trim(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Expenses.Add(expense);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Gasto creado: {ExpenseId} - {Amount}", expense.Id, expense.Amount);
        return expense;
    }

    public async Task UpdateExpenseAsync(
        Guid id,
        Guid? projectId,
        string description,
        decimal amount,
        string? category,
        DateTime expenseDate,
        string? notes)
    {
        if (string.IsNullOrWhiteSpace(description))
            throw new ArgumentException("La descripcion del gasto es obligatoria", nameof(description));

        if (amount <= 0)
            throw new ArgumentException("El monto debe ser mayor a cero", nameof(amount));

        var expense = await _context.Expenses.FindAsync(id)
            ?? throw new InvalidOperationException($"Gasto no encontrado: {id}");

        // Validar proyecto si se proporciona
        if (projectId.HasValue)
        {
            var projectExists = await _context.Projects.AnyAsync(p => p.Id == projectId.Value);
            if (!projectExists)
                throw new InvalidOperationException($"Proyecto no encontrado: {projectId}");
        }

        expense.ProjectId = projectId;
        expense.Description = description.Trim();
        expense.Amount = amount;
        expense.Category = category?.Trim();
        expense.ExpenseDate = expenseDate;
        expense.Notes = notes?.Trim();
        expense.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        _logger.LogInformation("Gasto actualizado: {ExpenseId}", id);
    }

    public async Task DeleteExpenseAsync(Guid id)
    {
        var expense = await _context.Expenses.FindAsync(id)
            ?? throw new InvalidOperationException($"Gasto no encontrado: {id}");

        _context.Expenses.Remove(expense);
        await _context.SaveChangesAsync();
        _logger.LogInformation("Gasto eliminado: {ExpenseId}", id);
    }
}
