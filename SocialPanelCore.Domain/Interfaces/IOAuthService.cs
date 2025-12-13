using SocialPanelCore.Domain.Enums;

namespace SocialPanelCore.Domain.Interfaces;

/// <summary>
/// Servicio para manejar flujos OAuth con redes sociales.
/// Soporta PKCE para proveedores que lo requieren (X/Twitter).
/// </summary>
public interface IOAuthService
{
    /// <summary>
    /// Genera la URL de autorización OAuth para una red social.
    /// </summary>
    /// <param name="networkType">Tipo de red social</param>
    /// <param name="state">State anti-CSRF generado por OAuthStateStore</param>
    /// <param name="redirectUri">URI de callback</param>
    /// <param name="codeChallenge">Code challenge para PKCE (opcional, requerido para X)</param>
    /// <returns>URL de autorización</returns>
    string GetAuthorizationUrl(NetworkType networkType, string state, string redirectUri, string? codeChallenge = null);

    /// <summary>
    /// Intercambia el código de autorización por tokens de acceso.
    /// </summary>
    /// <param name="networkType">Tipo de red social</param>
    /// <param name="code">Código de autorización</param>
    /// <param name="redirectUri">URI de callback usada en la autorización</param>
    /// <param name="codeVerifier">Code verifier para PKCE (requerido para X)</param>
    /// <returns>Resultado del intercambio con tokens</returns>
    Task<OAuthTokenResult> ExchangeCodeForTokensAsync(
        NetworkType networkType,
        string code,
        string redirectUri,
        string? codeVerifier = null);

    /// <summary>
    /// Renueva un token de acceso usando el refresh token.
    /// </summary>
    /// <param name="networkType">Tipo de red social</param>
    /// <param name="refreshToken">Refresh token actual</param>
    /// <returns>Resultado con nuevos tokens</returns>
    Task<OAuthTokenResult> RefreshTokenAsync(NetworkType networkType, string refreshToken);

    /// <summary>
    /// Obtiene información del usuario autenticado.
    /// </summary>
    Task<OAuthUserInfo?> GetUserInfoAsync(NetworkType networkType, string accessToken);

    /// <summary>
    /// Revoca tokens OAuth (si el proveedor lo soporta).
    /// </summary>
    /// <param name="networkType">Tipo de red social</param>
    /// <param name="accessToken">Token a revocar</param>
    /// <returns>True si se revocó correctamente</returns>
    Task<bool> RevokeTokenAsync(NetworkType networkType, string accessToken);

    /// <summary>
    /// Indica si el proveedor requiere PKCE.
    /// </summary>
    bool RequiresPkce(NetworkType networkType);

    /// <summary>
    /// Obtiene los scopes por defecto para un proveedor.
    /// </summary>
    string GetDefaultScopes(NetworkType networkType);
}

/// <summary>
/// Resultado del intercambio de tokens OAuth.
/// </summary>
public class OAuthTokenResult
{
    public bool Success { get; set; }
    public string? AccessToken { get; set; }
    public string? RefreshToken { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public DateTime? RefreshTokenExpiresAt { get; set; }
    public string? Scopes { get; set; }
    public string? Error { get; set; }
    public string? ErrorDescription { get; set; }
}

/// <summary>
/// Información del usuario obtenida via OAuth.
/// </summary>
public class OAuthUserInfo
{
    public string? Id { get; set; }
    public string? Username { get; set; }
    public string? DisplayName { get; set; }
    public string? ProfileImageUrl { get; set; }
}
