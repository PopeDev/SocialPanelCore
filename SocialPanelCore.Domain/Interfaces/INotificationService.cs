using SocialPanelCore.Domain.Entities;
using SocialPanelCore.Domain.Enums;

namespace SocialPanelCore.Domain.Interfaces;

public interface INotificationService
{
    // ========== Consultas ==========

    /// <summary>
    /// Obtiene las notificaciones no leídas para un usuario.
    /// </summary>
    Task<IEnumerable<Notification>> GetUnreadNotificationsAsync(Guid userId);

    /// <summary>
    /// Obtiene las notificaciones de una cuenta (para mostrar a todos los usuarios con acceso).
    /// </summary>
    Task<IEnumerable<Notification>> GetAccountNotificationsAsync(Guid accountId, bool includeRead = false);

    /// <summary>
    /// Obtiene las notificaciones de todas las cuentas a las que tiene acceso un usuario.
    /// </summary>
    Task<IEnumerable<Notification>> GetNotificationsForUserAsync(Guid userId, bool includeRead = false);

    /// <summary>
    /// Cuenta las notificaciones no leídas para un usuario.
    /// </summary>
    Task<int> GetUnreadCountAsync(Guid userId);

    /// <summary>
    /// Obtiene notificaciones que requieren acción (reconexión OAuth, etc.).
    /// </summary>
    Task<IEnumerable<Notification>> GetActionRequiredNotificationsAsync(Guid userId);

    // ========== Creación ==========

    /// <summary>
    /// Crea una notificación para una cuenta (visible para todos los usuarios con acceso).
    /// </summary>
    Task<Notification> CreateNotificationAsync(
        Guid accountId,
        NotificationType type,
        string title,
        string message,
        NetworkType? relatedNetwork = null,
        Guid? relatedChannelId = null,
        string? actionUrl = null,
        string? actionText = null,
        DateTime? expiresAt = null);

    /// <summary>
    /// Crea una notificación específica para un usuario.
    /// </summary>
    Task<Notification> CreateUserNotificationAsync(
        Guid userId,
        Guid accountId,
        NotificationType type,
        string title,
        string message,
        NetworkType? relatedNetwork = null,
        Guid? relatedChannelId = null,
        string? actionUrl = null,
        string? actionText = null);

    /// <summary>
    /// Crea una notificación de reconexión OAuth requerida.
    /// </summary>
    Task<Notification> CreateOAuthReauthNotificationAsync(
        Guid accountId,
        NetworkType network,
        Guid channelId,
        string errorCode);

    /// <summary>
    /// Crea una notificación de health check fallido.
    /// </summary>
    Task<Notification> CreateHealthCheckFailedNotificationAsync(
        Guid accountId,
        NetworkType network,
        Guid channelId,
        string errorMessage);

    // ========== Actualización ==========

    /// <summary>
    /// Marca una notificación como leída.
    /// </summary>
    Task MarkAsReadAsync(Guid notificationId);

    /// <summary>
    /// Marca todas las notificaciones de un usuario como leídas.
    /// </summary>
    Task MarkAllAsReadAsync(Guid userId);

    /// <summary>
    /// Descarta una notificación.
    /// </summary>
    Task DismissAsync(Guid notificationId);

    /// <summary>
    /// Descarta todas las notificaciones resueltas para un canal (cuando se reconecta exitosamente).
    /// </summary>
    Task DismissChannelNotificationsAsync(Guid channelId);

    // ========== Limpieza ==========

    /// <summary>
    /// Elimina notificaciones expiradas y antiguas.
    /// </summary>
    Task CleanupExpiredNotificationsAsync(int daysToKeep = 30);
}
