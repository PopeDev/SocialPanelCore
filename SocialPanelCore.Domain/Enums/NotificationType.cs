namespace SocialPanelCore.Domain.Enums;

/// <summary>
/// Tipo de notificación del sistema.
/// </summary>
public enum NotificationType
{
    /// <summary>
    /// Información general.
    /// </summary>
    Info = 0,

    /// <summary>
    /// Advertencia que requiere atención.
    /// </summary>
    Warning = 1,

    /// <summary>
    /// Error crítico.
    /// </summary>
    Error = 2,

    /// <summary>
    /// Operación exitosa.
    /// </summary>
    Success = 3,

    /// <summary>
    /// Se requiere reconexión OAuth.
    /// </summary>
    OAuthReauthRequired = 4,

    /// <summary>
    /// Token OAuth próximo a expirar.
    /// </summary>
    OAuthTokenExpiring = 5,

    /// <summary>
    /// Error de publicación en red social.
    /// </summary>
    PublishError = 6,

    /// <summary>
    /// Health check fallido.
    /// </summary>
    HealthCheckFailed = 7
}
