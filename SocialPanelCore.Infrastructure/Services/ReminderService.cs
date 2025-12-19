using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SocialPanelCore.Domain.Entities;
using SocialPanelCore.Domain.Interfaces;
using SocialPanelCore.Infrastructure.Data;

namespace SocialPanelCore.Infrastructure.Services;

public class ReminderService : IReminderService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<ReminderService> _logger;

    public ReminderService(ApplicationDbContext context, ILogger<ReminderService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<IEnumerable<Reminder>> GetAllRemindersAsync(Guid accountId)
    {
        _logger.LogInformation("Obteniendo recordatorios de cuenta: {AccountId}", accountId);
        return await _context.Reminders
            .AsNoTracking()
            .Include(r => r.Project)
            .Include(r => r.User)
            .Where(r => r.AccountId == accountId)
            .OrderBy(r => r.DueDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<Reminder>> GetRemindersByProjectAsync(Guid projectId)
    {
        return await _context.Reminders
            .AsNoTracking()
            .Include(r => r.User)
            .Where(r => r.ProjectId == projectId)
            .OrderBy(r => r.DueDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<Reminder>> GetRemindersByUserAsync(Guid userId)
    {
        return await _context.Reminders
            .AsNoTracking()
            .Include(r => r.Project)
            .Where(r => r.UserId == userId)
            .OrderBy(r => r.DueDate)
            .ToListAsync();
    }

    public async Task<IEnumerable<Reminder>> GetPendingRemindersAsync(Guid accountId)
    {
        return await _context.Reminders
            .AsNoTracking()
            .Include(r => r.Project)
            .Include(r => r.User)
            .Where(r => r.AccountId == accountId && !r.IsCompleted)
            .OrderBy(r => r.DueDate)
            .ToListAsync();
    }

    public async Task<Reminder?> GetReminderByIdAsync(Guid id)
    {
        return await _context.Reminders
            .AsNoTracking()
            .Include(r => r.Project)
            .Include(r => r.User)
            .FirstOrDefaultAsync(r => r.Id == id);
    }

    public async Task<Reminder> CreateReminderAsync(
        Guid accountId,
        Guid? userId,
        Guid? projectId,
        string title,
        string? description,
        DateTime dueDate)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("El titulo del recordatorio es obligatorio", nameof(title));

        var accountExists = await _context.Accounts.AnyAsync(a => a.Id == accountId);
        if (!accountExists)
            throw new InvalidOperationException($"Cuenta no encontrada: {accountId}");

        // Validar usuario si se proporciona
        if (userId.HasValue)
        {
            var userExists = await _context.Users.AnyAsync(u => u.Id == userId.Value);
            if (!userExists)
                throw new InvalidOperationException($"Usuario no encontrado: {userId}");
        }

        // Validar proyecto si se proporciona
        if (projectId.HasValue)
        {
            var projectExists = await _context.Projects.AnyAsync(p => p.Id == projectId.Value);
            if (!projectExists)
                throw new InvalidOperationException($"Proyecto no encontrado: {projectId}");
        }

        var reminder = new Reminder
        {
            Id = Guid.NewGuid(),
            AccountId = accountId,
            UserId = userId,
            ProjectId = projectId,
            Title = title.Trim(),
            Description = description?.Trim(),
            DueDate = dueDate,
            IsCompleted = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Reminders.Add(reminder);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Recordatorio creado: {ReminderId} - {Title}", reminder.Id, reminder.Title);
        return reminder;
    }

    public async Task UpdateReminderAsync(
        Guid id,
        Guid? userId,
        Guid? projectId,
        string title,
        string? description,
        DateTime dueDate)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("El titulo del recordatorio es obligatorio", nameof(title));

        var reminder = await _context.Reminders.FindAsync(id)
            ?? throw new InvalidOperationException($"Recordatorio no encontrado: {id}");

        // Validar usuario si se proporciona
        if (userId.HasValue)
        {
            var userExists = await _context.Users.AnyAsync(u => u.Id == userId.Value);
            if (!userExists)
                throw new InvalidOperationException($"Usuario no encontrado: {userId}");
        }

        // Validar proyecto si se proporciona
        if (projectId.HasValue)
        {
            var projectExists = await _context.Projects.AnyAsync(p => p.Id == projectId.Value);
            if (!projectExists)
                throw new InvalidOperationException($"Proyecto no encontrado: {projectId}");
        }

        reminder.UserId = userId;
        reminder.ProjectId = projectId;
        reminder.Title = title.Trim();
        reminder.Description = description?.Trim();
        reminder.DueDate = dueDate;
        reminder.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        _logger.LogInformation("Recordatorio actualizado: {ReminderId}", id);
    }

    public async Task MarkAsCompletedAsync(Guid id)
    {
        var reminder = await _context.Reminders.FindAsync(id)
            ?? throw new InvalidOperationException($"Recordatorio no encontrado: {id}");

        reminder.IsCompleted = true;
        reminder.CompletedAt = DateTime.UtcNow;
        reminder.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        _logger.LogInformation("Recordatorio completado: {ReminderId}", id);
    }

    public async Task MarkAsPendingAsync(Guid id)
    {
        var reminder = await _context.Reminders.FindAsync(id)
            ?? throw new InvalidOperationException($"Recordatorio no encontrado: {id}");

        reminder.IsCompleted = false;
        reminder.CompletedAt = null;
        reminder.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        _logger.LogInformation("Recordatorio marcado como pendiente: {ReminderId}", id);
    }

    public async Task DeleteReminderAsync(Guid id)
    {
        var reminder = await _context.Reminders.FindAsync(id)
            ?? throw new InvalidOperationException($"Recordatorio no encontrado: {id}");

        _context.Reminders.Remove(reminder);
        await _context.SaveChangesAsync();
        _logger.LogInformation("Recordatorio eliminado: {ReminderId}", id);
    }
}
