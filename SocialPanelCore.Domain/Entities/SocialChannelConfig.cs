using SocialPanelCore.Domain.Enums;

namespace SocialPanelCore.Domain.Entities;

public class SocialChannelConfig
{
    public Guid Id { get; set; }
    public Guid AccountId { get; set; }
    public NetworkType NetworkType { get; set; }

    /// <summary>
    /// Método de autenticación: OAuth o ApiKey
    /// </summary>
    public AuthMethod AuthMethod { get; set; } = AuthMethod.OAuth;

    // ========== Campos OAuth ==========
    /// <summary>
    /// Token de acceso OAuth (cifrado en BD)
    /// </summary>
    public string AccessToken { get; set; } = string.Empty;

    /// <summary>
    /// Token de refresco OAuth (cifrado en BD)
    /// </summary>
    public string? RefreshToken { get; set; }

    /// <summary>
    /// Fecha de expiración del access token (UTC)
    /// </summary>
    public DateTime? TokenExpiresAt { get; set; }

    /// <summary>
    /// Fecha de expiración del refresh token (UTC), si aplica.
    /// LinkedIn: ~1 año, X con offline.access: variable
    /// </summary>
    public DateTime? RefreshTokenExpiresAt { get; set; }

    /// <summary>
    /// Scopes autorizados por el usuario (comma-separated)
    /// </summary>
    public string? Scopes { get; set; }

    // ========== Estado de conexión OAuth ==========
    /// <summary>
    /// Estado de la conexión OAuth (Connected, NeedsReauth, Revoked, Error)
    /// </summary>
    public ConnectionStatus ConnectionStatus { get; set; } = ConnectionStatus.Connected;

    /// <summary>
    /// Último intento de refresh de tokens (UTC)
    /// </summary>
    public DateTime? LastRefreshAttemptAt { get; set; }

    /// <summary>
    /// Último refresh exitoso de tokens (UTC)
    /// </summary>
    public DateTime? LastRefreshSuccessAt { get; set; }

    /// <summary>
    /// Código del último error OAuth (ej: invalid_grant, token_expired)
    /// </summary>
    public string? LastOAuthErrorCode { get; set; }

    // ========== Campos ApiKey (X/Twitter, Telegram) ==========
    /// <summary>
    /// API Key / Consumer Key (cifrado en BD)
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// API Secret / Consumer Secret (cifrado en BD)
    /// </summary>
    public string? ApiSecret { get; set; }

    /// <summary>
    /// Access Token Secret para OAuth 1.0a (X/Twitter) (cifrado en BD)
    /// </summary>
    public string? AccessTokenSecret { get; set; }

    /// <summary>
    /// ID de chat/canal para Telegram
    /// </summary>
    public string? ExternalChannelId { get; set; }

    // ========== Campos comunes ==========
    /// <summary>
    /// Handle o nombre de usuario en la red (@usuario)
    /// </summary>
    public string? Handle { get; set; }

    /// <summary>
    /// ID externo del usuario/página en la red social
    /// </summary>
    public string? ExternalUserId { get; set; }

    public bool IsEnabled { get; set; }

    /// <summary>
    /// Indica si esta red social permite publicar con medios (imágenes/videos).
    /// Ejemplo: X/Twitter puede tener coste adicional por medios, entonces AllowMedia = false.
    /// Instagram normalmente requiere medios, entonces AllowMedia = true.
    /// </summary>
    public bool AllowMedia { get; set; } = true;

    public HealthStatus HealthStatus { get; set; }
    public DateTime? LastHealthCheck { get; set; }
    public string? LastErrorMessage { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Navegación
    public virtual Account Account { get; set; } = null!;
}
