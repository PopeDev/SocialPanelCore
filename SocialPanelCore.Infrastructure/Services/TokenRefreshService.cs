using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SocialPanelCore.Domain.Entities;
using SocialPanelCore.Domain.Enums;
using SocialPanelCore.Domain.Interfaces;
using SocialPanelCore.Infrastructure.Data;

namespace SocialPanelCore.Infrastructure.Services;

/// <summary>
/// Servicio para refrescar tokens de canales sociales automáticamente.
/// Soporta Facebook, Instagram y X (Twitter).
/// </summary>
public class TokenRefreshService : ITokenRefreshService
{
    private readonly ApplicationDbContext _context;
    private readonly IOAuthService _oauthService;
    private readonly INotificationService _notificationService;
    private readonly IDataProtector _protector;
    private readonly ILogger<TokenRefreshService> _logger;

    // Errores que indican que se requiere reautorización
    private static readonly HashSet<string> ReauthRequiredErrors = new(StringComparer.OrdinalIgnoreCase)
    {
        "invalid_grant",
        "token_expired",
        "token_revoked",
        "access_denied",
        "invalid_token",
        "190", // Facebook: token expired/invalid
        "463", // Facebook: token has been expired
        "467"  // Facebook: token has been invalidated
    };

    public TokenRefreshService(
        ApplicationDbContext context,
        IOAuthService oauthService,
        INotificationService notificationService,
        IDataProtectionProvider dataProtectionProvider,
        ILogger<TokenRefreshService> logger)
    {
        _context = context;
        _oauthService = oauthService;
        _notificationService = notificationService;
        _protector = dataProtectionProvider.CreateProtector("SocialChannelTokens");
        _logger = logger;
    }

    public async Task<TokenRefreshResult> RefreshTokensAsync(Guid channelId, CancellationToken cancellationToken = default)
    {
        var channel = await _context.SocialChannelConfigs.FindAsync(new object[] { channelId }, cancellationToken);
        if (channel == null)
        {
            return new TokenRefreshResult
            {
                Success = false,
                ErrorCode = "channel_not_found",
                ErrorMessage = $"Canal no encontrado: {channelId}"
            };
        }

        // No refrescar canales deshabilitados o con API Key
        if (!channel.IsEnabled || channel.AuthMethod == AuthMethod.ApiKey)
        {
            return new TokenRefreshResult
            {
                Success = false,
                ErrorCode = "not_applicable",
                ErrorMessage = "Canal deshabilitado o usa API Key"
            };
        }

        // No refrescar canales que ya requieren reauth
        if (channel.ConnectionStatus == ConnectionStatus.NeedsReauth ||
            channel.ConnectionStatus == ConnectionStatus.Revoked)
        {
            return new TokenRefreshResult
            {
                Success = false,
                RequiresReauth = true,
                ErrorCode = "needs_reauth",
                ErrorMessage = "Canal requiere reautorización"
            };
        }

        channel.LastRefreshAttemptAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);

        try
        {
            // Obtener token actual para refrescar
            var currentToken = _protector.Unprotect(channel.AccessToken);
            string? refreshToken = null;

            if (!string.IsNullOrEmpty(channel.RefreshToken))
            {
                refreshToken = _protector.Unprotect(channel.RefreshToken);
            }

            // Para Facebook/Instagram usamos el access token actual para obtener uno nuevo
            // Para X usamos el refresh token
            var tokenToRefresh = channel.NetworkType == NetworkType.X
                ? refreshToken ?? currentToken
                : currentToken;

            if (string.IsNullOrEmpty(tokenToRefresh))
            {
                await MarkNeedsReauthAsync(channelId, "no_token", "No hay token disponible para refrescar");
                return new TokenRefreshResult
                {
                    Success = false,
                    RequiresReauth = true,
                    ErrorCode = "no_token",
                    ErrorMessage = "No hay token disponible"
                };
            }

            _logger.LogDebug(
                "Refrescando token para canal {ChannelId} ({NetworkType})",
                channelId, channel.NetworkType);

            var result = await _oauthService.RefreshTokenAsync(channel.NetworkType, tokenToRefresh);

            if (!result.Success)
            {
                var requiresReauth = IsReauthRequiredError(result.Error);

                if (requiresReauth)
                {
                    await MarkNeedsReauthAsync(channelId, result.Error ?? "refresh_failed", result.ErrorDescription ?? "Error al refrescar token");
                }
                else
                {
                    // Error temporal, actualizar estado de salud
                    channel.HealthStatus = HealthStatus.KO;
                    channel.LastErrorMessage = result.ErrorDescription ?? result.Error;
                    channel.LastOAuthErrorCode = result.Error;
                    await _context.SaveChangesAsync(cancellationToken);
                }

                return new TokenRefreshResult
                {
                    Success = false,
                    RequiresReauth = requiresReauth,
                    ErrorCode = result.Error,
                    ErrorMessage = result.ErrorDescription
                };
            }

            // Actualizar tokens en la base de datos
            channel.AccessToken = _protector.Protect(result.AccessToken!);

            // X puede rotar el refresh token
            if (!string.IsNullOrEmpty(result.RefreshToken))
            {
                channel.RefreshToken = _protector.Protect(result.RefreshToken);
            }

            channel.TokenExpiresAt = result.ExpiresAt;
            channel.RefreshTokenExpiresAt = result.RefreshTokenExpiresAt;
            channel.LastRefreshSuccessAt = DateTime.UtcNow;
            channel.HealthStatus = HealthStatus.OK;
            channel.ConnectionStatus = ConnectionStatus.Connected;
            channel.LastErrorMessage = null;
            channel.LastOAuthErrorCode = null;
            channel.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Token refrescado exitosamente para canal {ChannelId} ({NetworkType}), nueva expiración: {ExpiresAt}",
                channelId, channel.NetworkType, result.ExpiresAt);

            return new TokenRefreshResult
            {
                Success = true,
                NewAccessToken = result.AccessToken,
                NewRefreshToken = result.RefreshToken,
                NewExpiresAt = result.ExpiresAt,
                NewRefreshTokenExpiresAt = result.RefreshTokenExpiresAt
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error inesperado refrescando token para canal {ChannelId}", channelId);

            channel.HealthStatus = HealthStatus.KO;
            channel.LastErrorMessage = ex.Message;
            channel.ConnectionStatus = ConnectionStatus.Error;
            await _context.SaveChangesAsync(cancellationToken);

            return new TokenRefreshResult
            {
                Success = false,
                ErrorCode = "unexpected_error",
                ErrorMessage = ex.Message
            };
        }
    }

    public async Task<int> RefreshExpiringTokensAsync(int safetyWindowMinutes = 30, CancellationToken cancellationToken = default)
    {
        var threshold = DateTime.UtcNow.AddMinutes(safetyWindowMinutes);

        // Buscar canales OAuth activos que expiran pronto
        var expiringChannels = await _context.SocialChannelConfigs
            .Where(c => c.IsEnabled
                && c.AuthMethod == AuthMethod.OAuth
                && c.ConnectionStatus == ConnectionStatus.Connected
                && c.TokenExpiresAt != null
                && c.TokenExpiresAt <= threshold)
            .Select(c => new { c.Id, c.NetworkType, c.TokenExpiresAt })
            .ToListAsync(cancellationToken);

        if (expiringChannels.Count == 0)
        {
            _logger.LogDebug("No hay tokens por expirar en los próximos {Minutes} minutos", safetyWindowMinutes);
            return 0;
        }

        _logger.LogInformation(
            "Encontrados {Count} canales con tokens por expirar, iniciando refresh",
            expiringChannels.Count);

        var successCount = 0;
        var reauthCount = 0;
        var errorCount = 0;

        foreach (var channel in expiringChannels)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            var result = await RefreshTokensAsync(channel.Id, cancellationToken);

            if (result.Success)
            {
                successCount++;
            }
            else if (result.RequiresReauth)
            {
                reauthCount++;
            }
            else
            {
                errorCount++;
            }

            // Pequeña pausa entre requests para no saturar las APIs
            await Task.Delay(100, cancellationToken);
        }

        _logger.LogInformation(
            "Refresh completado: {Success} exitosos, {Reauth} requieren reauth, {Errors} errores",
            successCount, reauthCount, errorCount);

        return successCount;
    }

    public async Task MarkNeedsReauthAsync(Guid channelId, string errorCode, string errorMessage)
    {
        var channel = await _context.SocialChannelConfigs.FindAsync(channelId);
        if (channel == null) return;

        channel.ConnectionStatus = ConnectionStatus.NeedsReauth;
        channel.HealthStatus = HealthStatus.KO;
        channel.LastOAuthErrorCode = errorCode;
        channel.LastErrorMessage = errorMessage;
        channel.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        _logger.LogWarning(
            "Canal {ChannelId} ({NetworkType}) marcado como NeedsReauth: {ErrorCode} - {ErrorMessage}",
            channelId, channel.NetworkType, errorCode, errorMessage);

        // Crear notificación in-app para los usuarios de la cuenta
        try
        {
            await _notificationService.CreateOAuthReauthNotificationAsync(
                channel.AccountId,
                channel.NetworkType,
                channelId,
                errorCode);

            _logger.LogInformation(
                "Notificación de reconexión creada para canal {ChannelId}",
                channelId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al crear notificación de reconexión para canal {ChannelId}", channelId);
        }
    }

    private static bool IsReauthRequiredError(string? errorCode)
    {
        if (string.IsNullOrEmpty(errorCode))
            return false;

        return ReauthRequiredErrors.Contains(errorCode);
    }
}
