using SocialPanelCore.Domain.Entities;
using SocialPanelCore.Domain.Enums;

namespace SocialPanelCore.Domain.Interfaces;

/// <summary>
/// Resultado del intento de refresh de tokens.
/// </summary>
public class TokenRefreshResult
{
    public bool Success { get; set; }
    public string? NewAccessToken { get; set; }
    public string? NewRefreshToken { get; set; }
    public DateTime? NewExpiresAt { get; set; }
    public DateTime? NewRefreshTokenExpiresAt { get; set; }

    /// <summary>
    /// Indica si se requiere que el usuario vuelva a autorizar.
    /// </summary>
    public bool RequiresReauth { get; set; }

    /// <summary>
    /// Código de error si falló.
    /// </summary>
    public string? ErrorCode { get; set; }

    /// <summary>
    /// Descripción del error.
    /// </summary>
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Servicio para refrescar tokens de canales sociales.
/// </summary>
public interface ITokenRefreshService
{
    /// <summary>
    /// Intenta refrescar los tokens de una conexión específica.
    /// </summary>
    /// <param name="channelId">ID del canal social</param>
    /// <param name="cancellationToken">Token de cancelación</param>
    /// <returns>Resultado del refresh</returns>
    Task<TokenRefreshResult> RefreshTokensAsync(Guid channelId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Procesa todos los canales que necesitan refresh.
    /// </summary>
    /// <param name="safetyWindowMinutes">Minutos antes de expiración para refrescar (default 30)</param>
    /// <param name="cancellationToken">Token de cancelación</param>
    /// <returns>Número de canales procesados exitosamente</returns>
    Task<int> RefreshExpiringTokensAsync(int safetyWindowMinutes = 30, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marca un canal como necesitando reautorización.
    /// </summary>
    Task MarkNeedsReauthAsync(Guid channelId, string errorCode, string errorMessage);
}
