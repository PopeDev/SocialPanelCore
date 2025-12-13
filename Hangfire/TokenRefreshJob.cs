using Microsoft.Extensions.Logging;
using SocialPanelCore.Domain.Interfaces;

namespace SocialPanelCore.Hangfire;

/// <summary>
/// Job de Hangfire para renovación automática de tokens OAuth.
/// Se ejecuta periódicamente para refrescar tokens antes de que expiren.
/// </summary>
public class TokenRefreshJob
{
    private readonly ITokenRefreshService _tokenRefreshService;
    private readonly IOAuthStateStore _oauthStateStore;
    private readonly ILogger<TokenRefreshJob> _logger;

    /// <summary>
    /// Ventana de seguridad: refrescar tokens que expiran en los próximos N minutos.
    /// </summary>
    private const int SafetyWindowMinutes = 30;

    public TokenRefreshJob(
        ITokenRefreshService tokenRefreshService,
        IOAuthStateStore oauthStateStore,
        ILogger<TokenRefreshJob> logger)
    {
        _tokenRefreshService = tokenRefreshService;
        _oauthStateStore = oauthStateStore;
        _logger = logger;
    }

    /// <summary>
    /// Ejecuta el refresh de tokens que están por expirar.
    /// Configurado para ejecutarse cada 15 minutos.
    /// </summary>
    public async Task RefreshExpiringTokensAsync()
    {
        _logger.LogInformation("Iniciando job de refresh de tokens OAuth");

        try
        {
            var refreshedCount = await _tokenRefreshService.RefreshExpiringTokensAsync(
                SafetyWindowMinutes,
                CancellationToken.None);

            _logger.LogInformation(
                "Job de refresh completado: {Count} tokens refrescados",
                refreshedCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en job de refresh de tokens");
            throw; // Hangfire reintentará el job
        }
    }

    /// <summary>
    /// Limpia estados OAuth expirados o consumidos.
    /// Configurado para ejecutarse cada hora.
    /// </summary>
    public async Task CleanupExpiredStatesAsync()
    {
        _logger.LogDebug("Iniciando limpieza de estados OAuth expirados");

        try
        {
            var cleanedCount = await _oauthStateStore.CleanupExpiredStatesAsync();

            if (cleanedCount > 0)
            {
                _logger.LogInformation(
                    "Limpiados {Count} estados OAuth expirados/consumidos",
                    cleanedCount);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en limpieza de estados OAuth");
            throw;
        }
    }
}
