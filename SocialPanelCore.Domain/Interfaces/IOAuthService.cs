using SocialPanelCore.Domain.Enums;

namespace SocialPanelCore.Domain.Interfaces;

/// <summary>
/// Servicio para manejar flujos OAuth con redes sociales.
/// </summary>
public interface IOAuthService
{
    /// <summary>
    /// Genera la URL de autorización OAuth para una red social.
    /// </summary>
    /// <param name="networkType">Tipo de red social</param>
    /// <param name="accountId">ID de la cuenta a la que se asociará</param>
    /// <param name="redirectUri">URI de callback</param>
    /// <returns>URL de autorización</returns>
    string GetAuthorizationUrl(NetworkType networkType, Guid accountId, string redirectUri);

    /// <summary>
    /// Intercambia el código de autorización por tokens de acceso.
    /// </summary>
    /// <param name="networkType">Tipo de red social</param>
    /// <param name="code">Código de autorización</param>
    /// <param name="redirectUri">URI de callback usada en la autorización</param>
    /// <returns>Resultado del intercambio con tokens</returns>
    Task<OAuthTokenResult> ExchangeCodeForTokensAsync(NetworkType networkType, string code, string redirectUri);

    /// <summary>
    /// Renueva un token de acceso usando el refresh token.
    /// </summary>
    Task<OAuthTokenResult> RefreshTokenAsync(NetworkType networkType, string refreshToken);

    /// <summary>
    /// Obtiene información del usuario autenticado.
    /// </summary>
    Task<OAuthUserInfo?> GetUserInfoAsync(NetworkType networkType, string accessToken);
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
