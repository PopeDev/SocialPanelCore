using SocialPanelCore.Domain.Entities;

namespace SocialPanelCore.Domain.Interfaces;

public interface IReminderService
{
    Task<IEnumerable<Reminder>> GetAllRemindersAsync(Guid accountId);
    Task<IEnumerable<Reminder>> GetRemindersByProjectAsync(Guid projectId);
    Task<IEnumerable<Reminder>> GetRemindersByUserAsync(Guid userId);
    Task<IEnumerable<Reminder>> GetPendingRemindersAsync(Guid accountId);
    Task<Reminder?> GetReminderByIdAsync(Guid id);
    Task<Reminder> CreateReminderAsync(
        Guid accountId,
        Guid? userId,
        Guid? projectId,
        string title,
        string? description,
        DateTime dueDate);
    Task UpdateReminderAsync(
        Guid id,
        Guid? userId,
        Guid? projectId,
        string title,
        string? description,
        DateTime dueDate);
    Task MarkAsCompletedAsync(Guid id);
    Task MarkAsPendingAsync(Guid id);
    Task DeleteReminderAsync(Guid id);
}
