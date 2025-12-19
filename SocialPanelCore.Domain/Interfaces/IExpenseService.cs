using SocialPanelCore.Domain.Entities;

namespace SocialPanelCore.Domain.Interfaces;

public interface IExpenseService
{
    Task<IEnumerable<Expense>> GetAllExpensesAsync(Guid accountId);
    Task<IEnumerable<Expense>> GetExpensesByProjectAsync(Guid projectId);
    Task<Expense?> GetExpenseByIdAsync(Guid id);
    Task<Expense> CreateExpenseAsync(
        Guid accountId,
        Guid? projectId,
        string description,
        decimal amount,
        string? category,
        DateTime expenseDate,
        string? notes);
    Task UpdateExpenseAsync(
        Guid id,
        Guid? projectId,
        string description,
        decimal amount,
        string? category,
        DateTime expenseDate,
        string? notes);
    Task DeleteExpenseAsync(Guid id);
}
