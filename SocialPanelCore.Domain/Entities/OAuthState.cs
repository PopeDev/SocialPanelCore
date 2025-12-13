using SocialPanelCore.Domain.Enums;

namespace SocialPanelCore.Domain.Entities;

/// <summary>
/// Almacena el estado OAuth temporalmente durante el flujo de autorización.
/// Incluye state anti-CSRF y code_verifier para PKCE.
/// Se elimina automáticamente tras usar o expirar.
/// </summary>
public class OAuthState
{
    public Guid Id { get; set; }

    /// <summary>
    /// Valor del parámetro state enviado en la URL de autorización.
    /// Se usa para validar el callback y prevenir CSRF.
    /// </summary>
    public string State { get; set; } = string.Empty;

    /// <summary>
    /// ID de la cuenta (tenant) a la que se asociará la conexión.
    /// </summary>
    public Guid AccountId { get; set; }

    /// <summary>
    /// ID del usuario que inició el flujo OAuth.
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Red social del flujo OAuth.
    /// </summary>
    public NetworkType NetworkType { get; set; }

    /// <summary>
    /// URL de redirección usada en el authorize.
    /// Se reutiliza en el token exchange.
    /// </summary>
    public string RedirectUri { get; set; } = string.Empty;

    /// <summary>
    /// URL a la que redirigir al usuario tras completar OAuth.
    /// </summary>
    public string? ReturnUrl { get; set; }

    /// <summary>
    /// Code verifier para PKCE (se guarda aquí, se envía en token exchange).
    /// Solo para proveedores que soportan PKCE (X/Twitter, etc.)
    /// </summary>
    public string? CodeVerifier { get; set; }

    /// <summary>
    /// Scopes solicitados en la autorización.
    /// </summary>
    public string? RequestedScopes { get; set; }

    /// <summary>
    /// Fecha de creación del state.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Fecha de expiración del state (típicamente 10-15 minutos).
    /// </summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>
    /// Indica si el state ya fue consumido (usado en callback).
    /// </summary>
    public bool IsConsumed { get; set; }
}
