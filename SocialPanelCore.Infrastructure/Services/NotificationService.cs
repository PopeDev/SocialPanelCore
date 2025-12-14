using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SocialPanelCore.Domain.Entities;
using SocialPanelCore.Domain.Enums;
using SocialPanelCore.Domain.Interfaces;
using SocialPanelCore.Infrastructure.Data;

namespace SocialPanelCore.Infrastructure.Services;

public class NotificationService : INotificationService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(
        ApplicationDbContext context,
        ILogger<NotificationService> logger)
    {
        _context = context;
        _logger = logger;
    }

    // ========== Consultas ==========

    public async Task<IEnumerable<Notification>> GetUnreadNotificationsAsync(Guid userId)
    {
        // Obtener las cuentas a las que tiene acceso el usuario
        var accountIds = await _context.UserAccountAccess
            .Where(ua => ua.UserId == userId)
            .Select(ua => ua.AccountId)
            .ToListAsync();

        return await _context.Notifications
            .AsNoTracking()
            .Where(n => !n.IsRead && !n.IsDismissed)
            .Where(n => n.UserId == userId || (n.UserId == null && accountIds.Contains(n.AccountId)))
            .Where(n => n.ExpiresAt == null || n.ExpiresAt > DateTime.UtcNow)
            .OrderByDescending(n => n.CreatedAt)
            .Take(50)
            .ToListAsync();
    }

    public async Task<IEnumerable<Notification>> GetAccountNotificationsAsync(Guid accountId, bool includeRead = false)
    {
        var query = _context.Notifications
            .AsNoTracking()
            .Where(n => n.AccountId == accountId && !n.IsDismissed)
            .Where(n => n.ExpiresAt == null || n.ExpiresAt > DateTime.UtcNow);

        if (!includeRead)
        {
            query = query.Where(n => !n.IsRead);
        }

        return await query
            .OrderByDescending(n => n.CreatedAt)
            .Take(100)
            .ToListAsync();
    }

    public async Task<IEnumerable<Notification>> GetNotificationsForUserAsync(Guid userId, bool includeRead = false)
    {
        // Obtener las cuentas a las que tiene acceso el usuario
        var accountIds = await _context.UserAccountAccess
            .Where(ua => ua.UserId == userId)
            .Select(ua => ua.AccountId)
            .ToListAsync();

        var query = _context.Notifications
            .AsNoTracking()
            .Where(n => !n.IsDismissed)
            .Where(n => n.UserId == userId || (n.UserId == null && accountIds.Contains(n.AccountId)))
            .Where(n => n.ExpiresAt == null || n.ExpiresAt > DateTime.UtcNow);

        if (!includeRead)
        {
            query = query.Where(n => !n.IsRead);
        }

        return await query
            .OrderByDescending(n => n.CreatedAt)
            .Take(100)
            .ToListAsync();
    }

    public async Task<int> GetUnreadCountAsync(Guid userId)
    {
        // Obtener las cuentas a las que tiene acceso el usuario
        var accountIds = await _context.UserAccountAccess
            .Where(ua => ua.UserId == userId)
            .Select(ua => ua.AccountId)
            .ToListAsync();

        return await _context.Notifications
            .Where(n => !n.IsRead && !n.IsDismissed)
            .Where(n => n.UserId == userId || (n.UserId == null && accountIds.Contains(n.AccountId)))
            .Where(n => n.ExpiresAt == null || n.ExpiresAt > DateTime.UtcNow)
            .CountAsync();
    }

    public async Task<IEnumerable<Notification>> GetActionRequiredNotificationsAsync(Guid userId)
    {
        // Obtener las cuentas a las que tiene acceso el usuario
        var accountIds = await _context.UserAccountAccess
            .Where(ua => ua.UserId == userId)
            .Select(ua => ua.AccountId)
            .ToListAsync();

        var actionTypes = new[]
        {
            NotificationType.OAuthReauthRequired,
            NotificationType.OAuthTokenExpiring,
            NotificationType.HealthCheckFailed
        };

        return await _context.Notifications
            .AsNoTracking()
            .Where(n => !n.IsDismissed && actionTypes.Contains(n.Type))
            .Where(n => n.UserId == userId || (n.UserId == null && accountIds.Contains(n.AccountId)))
            .Where(n => n.ExpiresAt == null || n.ExpiresAt > DateTime.UtcNow)
            .OrderByDescending(n => n.Type == NotificationType.OAuthReauthRequired)
            .ThenByDescending(n => n.CreatedAt)
            .ToListAsync();
    }

    // ========== Creación ==========

    public async Task<Notification> CreateNotificationAsync(
        Guid accountId,
        NotificationType type,
        string title,
        string message,
        NetworkType? relatedNetwork = null,
        Guid? relatedChannelId = null,
        string? actionUrl = null,
        string? actionText = null,
        DateTime? expiresAt = null)
    {
        // Evitar duplicados de notificaciones de reconexión activas
        if (type == NotificationType.OAuthReauthRequired && relatedChannelId.HasValue)
        {
            var existingNotification = await _context.Notifications
                .FirstOrDefaultAsync(n =>
                    n.RelatedChannelId == relatedChannelId &&
                    n.Type == NotificationType.OAuthReauthRequired &&
                    !n.IsDismissed);

            if (existingNotification != null)
            {
                _logger.LogDebug(
                    "Ya existe una notificación de reconexión para el canal {ChannelId}",
                    relatedChannelId);
                return existingNotification;
            }
        }

        var notification = new Notification
        {
            Id = Guid.NewGuid(),
            AccountId = accountId,
            Type = type,
            Title = title,
            Message = message,
            RelatedNetwork = relatedNetwork,
            RelatedChannelId = relatedChannelId,
            ActionUrl = actionUrl,
            ActionText = actionText,
            ExpiresAt = expiresAt,
            CreatedAt = DateTime.UtcNow
        };

        _context.Notifications.Add(notification);
        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "Notificación creada: {Type} para cuenta {AccountId} - {Title}",
            type, accountId, title);

        return notification;
    }

    public async Task<Notification> CreateUserNotificationAsync(
        Guid userId,
        Guid accountId,
        NotificationType type,
        string title,
        string message,
        NetworkType? relatedNetwork = null,
        Guid? relatedChannelId = null,
        string? actionUrl = null,
        string? actionText = null)
    {
        var notification = new Notification
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            AccountId = accountId,
            Type = type,
            Title = title,
            Message = message,
            RelatedNetwork = relatedNetwork,
            RelatedChannelId = relatedChannelId,
            ActionUrl = actionUrl,
            ActionText = actionText,
            CreatedAt = DateTime.UtcNow
        };

        _context.Notifications.Add(notification);
        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "Notificación de usuario creada: {Type} para usuario {UserId} - {Title}",
            type, userId, title);

        return notification;
    }

    public async Task<Notification> CreateOAuthReauthNotificationAsync(
        Guid accountId,
        NetworkType network,
        Guid channelId,
        string errorCode)
    {
        var networkName = GetNetworkName(network);

        return await CreateNotificationAsync(
            accountId,
            NotificationType.OAuthReauthRequired,
            $"Reconexión requerida: {networkName}",
            $"Se requiere reconectar la cuenta de {networkName}. " +
            $"El token de acceso ha expirado o fue revocado. Código de error: {errorCode}",
            network,
            channelId,
            $"/social-channels?reconnect={channelId}",
            "Reconectar ahora");
    }

    public async Task<Notification> CreateHealthCheckFailedNotificationAsync(
        Guid accountId,
        NetworkType network,
        Guid channelId,
        string errorMessage)
    {
        var networkName = GetNetworkName(network);

        // Evitar duplicados de notificaciones de health check activas
        var existingNotification = await _context.Notifications
            .FirstOrDefaultAsync(n =>
                n.RelatedChannelId == channelId &&
                n.Type == NotificationType.HealthCheckFailed &&
                !n.IsDismissed &&
                n.CreatedAt > DateTime.UtcNow.AddHours(-1)); // Solo si fue creada en la última hora

        if (existingNotification != null)
        {
            _logger.LogDebug(
                "Ya existe una notificación de health check reciente para el canal {ChannelId}",
                channelId);
            return existingNotification;
        }

        return await CreateNotificationAsync(
            accountId,
            NotificationType.HealthCheckFailed,
            $"Error de conexión: {networkName}",
            $"No se pudo verificar la conexión con {networkName}. {errorMessage}",
            network,
            channelId,
            $"/social-channels",
            "Ver detalles",
            DateTime.UtcNow.AddDays(7)); // Expira en 7 días
    }

    // ========== Actualización ==========

    public async Task MarkAsReadAsync(Guid notificationId)
    {
        var notification = await _context.Notifications.FindAsync(notificationId);
        if (notification != null && !notification.IsRead)
        {
            notification.IsRead = true;
            notification.ReadAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }
    }

    public async Task MarkAllAsReadAsync(Guid userId)
    {
        // Obtener las cuentas a las que tiene acceso el usuario
        var accountIds = await _context.UserAccountAccess
            .Where(ua => ua.UserId == userId)
            .Select(ua => ua.AccountId)
            .ToListAsync();

        var notifications = await _context.Notifications
            .Where(n => !n.IsRead && !n.IsDismissed)
            .Where(n => n.UserId == userId || (n.UserId == null && accountIds.Contains(n.AccountId)))
            .ToListAsync();

        var now = DateTime.UtcNow;
        foreach (var notification in notifications)
        {
            notification.IsRead = true;
            notification.ReadAt = now;
        }

        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "Marcadas {Count} notificaciones como leídas para usuario {UserId}",
            notifications.Count, userId);
    }

    public async Task DismissAsync(Guid notificationId)
    {
        var notification = await _context.Notifications.FindAsync(notificationId);
        if (notification != null && !notification.IsDismissed)
        {
            notification.IsDismissed = true;
            await _context.SaveChangesAsync();
        }
    }

    public async Task DismissChannelNotificationsAsync(Guid channelId)
    {
        var notifications = await _context.Notifications
            .Where(n => n.RelatedChannelId == channelId && !n.IsDismissed)
            .Where(n => n.Type == NotificationType.OAuthReauthRequired ||
                       n.Type == NotificationType.HealthCheckFailed ||
                       n.Type == NotificationType.OAuthTokenExpiring)
            .ToListAsync();

        foreach (var notification in notifications)
        {
            notification.IsDismissed = true;
        }

        await _context.SaveChangesAsync();

        if (notifications.Count > 0)
        {
            _logger.LogInformation(
                "Descartadas {Count} notificaciones para canal {ChannelId}",
                notifications.Count, channelId);
        }
    }

    // ========== Limpieza ==========

    public async Task CleanupExpiredNotificationsAsync(int daysToKeep = 30)
    {
        var cutoffDate = DateTime.UtcNow.AddDays(-daysToKeep);

        // Eliminar notificaciones expiradas
        var expiredNotifications = await _context.Notifications
            .Where(n => (n.ExpiresAt != null && n.ExpiresAt < DateTime.UtcNow) ||
                       (n.IsDismissed && n.CreatedAt < cutoffDate) ||
                       (n.IsRead && n.CreatedAt < cutoffDate))
            .ToListAsync();

        if (expiredNotifications.Count > 0)
        {
            _context.Notifications.RemoveRange(expiredNotifications);
            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Eliminadas {Count} notificaciones expiradas/antiguas",
                expiredNotifications.Count);
        }
    }

    // ========== Helpers ==========

    private static string GetNetworkName(NetworkType network) => network switch
    {
        NetworkType.Facebook => "Facebook",
        NetworkType.Instagram => "Instagram",
        NetworkType.X => "X (Twitter)",
        NetworkType.LinkedIn => "LinkedIn",
        NetworkType.TikTok => "TikTok",
        NetworkType.YouTube => "YouTube",
        _ => network.ToString()
    };
}
