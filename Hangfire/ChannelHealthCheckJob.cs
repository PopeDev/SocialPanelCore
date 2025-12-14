using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SocialPanelCore.Domain.Enums;
using SocialPanelCore.Domain.Interfaces;
using SocialPanelCore.Infrastructure.Data;

namespace SocialPanelCore.Hangfire;

/// <summary>
/// Job de Hangfire para verificación periódica de salud de canales sociales.
/// Verifica que las conexiones OAuth siguen funcionando correctamente.
/// </summary>
public class ChannelHealthCheckJob
{
    private readonly ApplicationDbContext _context;
    private readonly IOAuthService _oauthService;
    private readonly ISocialChannelConfigService _channelConfigService;
    private readonly INotificationService _notificationService;
    private readonly ILogger<ChannelHealthCheckJob> _logger;

    public ChannelHealthCheckJob(
        ApplicationDbContext context,
        IOAuthService oauthService,
        ISocialChannelConfigService channelConfigService,
        INotificationService notificationService,
        ILogger<ChannelHealthCheckJob> logger)
    {
        _context = context;
        _oauthService = oauthService;
        _channelConfigService = channelConfigService;
        _notificationService = notificationService;
        _logger = logger;
    }

    /// <summary>
    /// Verifica la salud de todos los canales sociales activos.
    /// Configurado para ejecutarse cada hora.
    /// </summary>
    public async Task CheckChannelHealthAsync()
    {
        _logger.LogInformation("Iniciando job de health check de canales sociales");

        var channels = await _context.SocialChannelConfigs
            .Where(c => c.IsEnabled)
            .Where(c => c.ConnectionStatus == ConnectionStatus.Connected)
            .Where(c => c.AuthMethod == AuthMethod.OAuth)
            .ToListAsync();

        _logger.LogInformation(
            "Verificando salud de {Count} canales activos",
            channels.Count);

        var healthyCount = 0;
        var unhealthyCount = 0;

        foreach (var channel in channels)
        {
            try
            {
                // Solo verificar si no se ha verificado en la última hora
                if (channel.LastHealthCheck.HasValue &&
                    channel.LastHealthCheck.Value > DateTime.UtcNow.AddHours(-1))
                {
                    continue;
                }

                var credentials = await _channelConfigService.GetDecryptedOAuthCredentialsAsync(channel.Id);
                if (credentials == null)
                {
                    _logger.LogWarning(
                        "No se pudieron obtener credenciales para canal {ChannelId}",
                        channel.Id);
                    continue;
                }

                var (isHealthy, errorMessage) = await CheckChannelHealthAsync(
                    channel.NetworkType,
                    credentials.Value.AccessToken);

                if (isHealthy)
                {
                    await _channelConfigService.UpdateHealthStatusAsync(channel.Id, HealthStatus.OK);
                    healthyCount++;

                    // Descartar notificaciones de error anteriores si la conexión está OK
                    await _notificationService.DismissChannelNotificationsAsync(channel.Id);
                }
                else
                {
                    await _channelConfigService.UpdateHealthStatusAsync(
                        channel.Id, HealthStatus.KO, errorMessage);
                    unhealthyCount++;

                    // Crear notificación de error si el canal tiene problemas
                    await _notificationService.CreateHealthCheckFailedNotificationAsync(
                        channel.AccountId,
                        channel.NetworkType,
                        channel.Id,
                        errorMessage ?? "Error desconocido al verificar la conexión");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Error al verificar salud del canal {ChannelId} ({Network})",
                    channel.Id, channel.NetworkType);

                await _channelConfigService.UpdateHealthStatusAsync(
                    channel.Id, HealthStatus.KO, ex.Message);
                unhealthyCount++;
            }
        }

        _logger.LogInformation(
            "Health check completado: {Healthy} OK, {Unhealthy} con problemas",
            healthyCount, unhealthyCount);
    }

    /// <summary>
    /// Verifica la salud de un canal específico llamando a la API del proveedor.
    /// </summary>
    private async Task<(bool IsHealthy, string? ErrorMessage)> CheckChannelHealthAsync(
        NetworkType network,
        string accessToken)
    {
        try
        {
            var userInfo = await _oauthService.GetUserInfoAsync(network, accessToken);

            if (userInfo == null)
            {
                return (false, "No se pudo obtener información del usuario");
            }

            return (true, null);
        }
        catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            return (false, "Token de acceso inválido o expirado");
        }
        catch (HttpRequestException ex)
        {
            return (false, $"Error de conexión: {ex.Message}");
        }
        catch (Exception ex)
        {
            return (false, $"Error inesperado: {ex.Message}");
        }
    }

    /// <summary>
    /// Limpia las notificaciones expiradas y antiguas.
    /// Configurado para ejecutarse cada día a las 3:00 AM.
    /// </summary>
    public async Task CleanupExpiredNotificationsAsync()
    {
        _logger.LogDebug("Iniciando limpieza de notificaciones expiradas");

        try
        {
            await _notificationService.CleanupExpiredNotificationsAsync(daysToKeep: 30);
            _logger.LogInformation("Limpieza de notificaciones completada");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en limpieza de notificaciones");
            throw;
        }
    }
}
