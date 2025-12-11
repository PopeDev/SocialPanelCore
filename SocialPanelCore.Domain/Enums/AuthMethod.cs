namespace SocialPanelCore.Domain.Enums;

/// <summary>
/// Método de autenticación utilizado para conectar con una red social.
/// </summary>
public enum AuthMethod
{
    /// <summary>
    /// Autenticación mediante flujo OAuth 2.0 (Facebook, Instagram, LinkedIn, YouTube)
    /// </summary>
    OAuth = 0,

    /// <summary>
    /// Autenticación mediante API Keys y tokens de acceso (X/Twitter, Telegram)
    /// </summary>
    ApiKey = 1
}
