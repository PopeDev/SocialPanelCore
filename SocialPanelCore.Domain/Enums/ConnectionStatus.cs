namespace SocialPanelCore.Domain.Enums;

/// <summary>
/// Estado de la conexión OAuth de un canal social.
/// </summary>
public enum ConnectionStatus
{
    /// <summary>
    /// Conexión activa y funcionando correctamente.
    /// </summary>
    Connected = 0,

    /// <summary>
    /// Se requiere que el usuario vuelva a autorizar la conexión.
    /// Ocurre cuando el refresh token expira o es revocado.
    /// </summary>
    NeedsReauth = 1,

    /// <summary>
    /// El usuario revocó el acceso desde la red social.
    /// </summary>
    Revoked = 2,

    /// <summary>
    /// Error al intentar refrescar o usar los tokens.
    /// </summary>
    Error = 3,

    /// <summary>
    /// Conexión en proceso de configuración inicial.
    /// </summary>
    Pending = 4
}
