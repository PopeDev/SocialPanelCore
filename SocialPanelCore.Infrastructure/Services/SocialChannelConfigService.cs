using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SocialPanelCore.Domain.Entities;
using SocialPanelCore.Domain.Enums;
using SocialPanelCore.Domain.Interfaces;
using SocialPanelCore.Infrastructure.Data;

namespace SocialPanelCore.Infrastructure.Services;

public class SocialChannelConfigService : ISocialChannelConfigService
{
    private readonly ApplicationDbContext _context;
    private readonly IDataProtector _protector;
    private readonly ILogger<SocialChannelConfigService> _logger;

    public SocialChannelConfigService(
        ApplicationDbContext context,
        IDataProtectionProvider dataProtectionProvider,
        ILogger<SocialChannelConfigService> logger)
    {
        _context = context;
        _protector = dataProtectionProvider.CreateProtector("SocialChannelTokens");
        _logger = logger;
    }

    public async Task<IEnumerable<SocialChannelConfig>> GetChannelConfigsByAccountAsync(Guid accountId)
    {
        var channels = await _context.SocialChannelConfigs
            .AsNoTracking()
            .Where(c => c.AccountId == accountId)
            .OrderBy(c => c.NetworkType)
            .ToListAsync();

        // No exponer tokens en la respuesta
        foreach (var channel in channels)
        {
            channel.AccessToken = "***PROTECTED***";
            channel.RefreshToken = null;
            channel.ApiKey = channel.ApiKey != null ? "***PROTECTED***" : null;
            channel.ApiSecret = null;
            channel.AccessTokenSecret = null;
        }

        return channels;
    }

    public async Task<SocialChannelConfig?> GetChannelConfigAsync(Guid id)
    {
        return await _context.SocialChannelConfigs.FindAsync(id);
    }

    public async Task<SocialChannelConfig?> GetChannelConfigByAccountAndNetworkAsync(Guid accountId, NetworkType networkType)
    {
        return await _context.SocialChannelConfigs
            .FirstOrDefaultAsync(c => c.AccountId == accountId && c.NetworkType == networkType);
    }

    // ========== Creación con OAuth ==========
    public async Task<SocialChannelConfig> CreateChannelConfigAsync(
        Guid accountId,
        NetworkType networkType,
        string accessToken,
        string? refreshToken,
        DateTime? tokenExpiresAt,
        string? handle)
    {
        // Verificar que la cuenta existe
        var accountExists = await _context.Accounts.AnyAsync(a => a.Id == accountId);
        if (!accountExists)
            throw new InvalidOperationException($"Cuenta no encontrada: {accountId}");

        // Verificar que no existe ya una configuración para esta red
        var existingConfig = await _context.SocialChannelConfigs
            .FirstOrDefaultAsync(c => c.AccountId == accountId && c.NetworkType == networkType);

        if (existingConfig != null)
            throw new InvalidOperationException(
                $"Ya existe una configuración de {networkType} para esta cuenta");

        var config = new SocialChannelConfig
        {
            Id = Guid.NewGuid(),
            AccountId = accountId,
            NetworkType = networkType,
            AuthMethod = AuthMethod.OAuth,
            AccessToken = _protector.Protect(accessToken),
            RefreshToken = refreshToken != null ? _protector.Protect(refreshToken) : null,
            TokenExpiresAt = tokenExpiresAt,
            Handle = handle,
            IsEnabled = true,
            HealthStatus = HealthStatus.OK,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.SocialChannelConfigs.Add(config);
        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "Canal social configurado (OAuth): {NetworkType} para cuenta {AccountId}",
            networkType, accountId);

        return config;
    }

    // ========== Creación con ApiKey (X/Twitter, Telegram) ==========
    public async Task<SocialChannelConfig> CreateChannelConfigWithApiKeyAsync(
        Guid accountId,
        NetworkType networkType,
        string apiKey,
        string apiSecret,
        string accessToken,
        string accessTokenSecret,
        string? handle)
    {
        // Verificar que la cuenta existe
        var accountExists = await _context.Accounts.AnyAsync(a => a.Id == accountId);
        if (!accountExists)
            throw new InvalidOperationException($"Cuenta no encontrada: {accountId}");

        // Verificar que no existe ya una configuración para esta red
        var existingConfig = await _context.SocialChannelConfigs
            .FirstOrDefaultAsync(c => c.AccountId == accountId && c.NetworkType == networkType);

        if (existingConfig != null)
        {
            // Si existe, actualizar en lugar de crear
            await UpdateApiKeyCredentialsAsync(existingConfig.Id, apiKey, apiSecret, accessToken, accessTokenSecret);
            if (handle != null)
            {
                existingConfig.Handle = handle;
            }
            existingConfig.IsEnabled = true;
            existingConfig.HealthStatus = HealthStatus.OK;
            existingConfig.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Canal social actualizado (ApiKey): {NetworkType} para cuenta {AccountId}",
                networkType, accountId);

            return existingConfig;
        }

        var config = new SocialChannelConfig
        {
            Id = Guid.NewGuid(),
            AccountId = accountId,
            NetworkType = networkType,
            AuthMethod = AuthMethod.ApiKey,
            ApiKey = _protector.Protect(apiKey),
            ApiSecret = _protector.Protect(apiSecret),
            AccessToken = _protector.Protect(accessToken),
            AccessTokenSecret = _protector.Protect(accessTokenSecret),
            Handle = handle,
            IsEnabled = true,
            HealthStatus = HealthStatus.OK,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.SocialChannelConfigs.Add(config);
        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "Canal social configurado (ApiKey): {NetworkType} para cuenta {AccountId}",
            networkType, accountId);

        return config;
    }

    // ========== Actualización OAuth ==========
    public async Task UpdateTokensAsync(Guid id, string accessToken, string? refreshToken, DateTime? tokenExpiresAt)
    {
        var config = await _context.SocialChannelConfigs.FindAsync(id)
            ?? throw new InvalidOperationException($"Configuración no encontrada: {id}");

        config.AccessToken = _protector.Protect(accessToken);
        config.RefreshToken = refreshToken != null ? _protector.Protect(refreshToken) : null;
        config.TokenExpiresAt = tokenExpiresAt;
        config.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        _logger.LogInformation("Tokens OAuth actualizados para canal {ChannelId}", id);
    }

    // ========== Actualización ApiKey ==========
    public async Task UpdateApiKeyCredentialsAsync(Guid id, string apiKey, string apiSecret, string accessToken, string accessTokenSecret)
    {
        var config = await _context.SocialChannelConfigs.FindAsync(id)
            ?? throw new InvalidOperationException($"Configuración no encontrada: {id}");

        config.ApiKey = _protector.Protect(apiKey);
        config.ApiSecret = _protector.Protect(apiSecret);
        config.AccessToken = _protector.Protect(accessToken);
        config.AccessTokenSecret = _protector.Protect(accessTokenSecret);
        config.AuthMethod = AuthMethod.ApiKey;
        config.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        _logger.LogInformation("Credenciales ApiKey actualizadas para canal {ChannelId}", id);
    }

    public async Task EnableChannelAsync(Guid id)
    {
        var config = await _context.SocialChannelConfigs.FindAsync(id)
            ?? throw new InvalidOperationException($"Configuración no encontrada: {id}");

        config.IsEnabled = true;
        config.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        _logger.LogInformation("Canal habilitado: {ChannelId}", id);
    }

    public async Task DisableChannelAsync(Guid id)
    {
        var config = await _context.SocialChannelConfigs.FindAsync(id)
            ?? throw new InvalidOperationException($"Configuración no encontrada: {id}");

        config.IsEnabled = false;
        config.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        _logger.LogInformation("Canal deshabilitado: {ChannelId}", id);
    }

    public async Task UpdateHealthStatusAsync(Guid id, HealthStatus status, string? errorMessage = null)
    {
        var config = await _context.SocialChannelConfigs.FindAsync(id)
            ?? throw new InvalidOperationException($"Configuración no encontrada: {id}");

        config.HealthStatus = status;
        config.LastHealthCheck = DateTime.UtcNow;
        config.LastErrorMessage = errorMessage;
        config.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
    }

    public async Task DeleteChannelAsync(Guid id)
    {
        var config = await _context.SocialChannelConfigs.FindAsync(id)
            ?? throw new InvalidOperationException($"Configuración no encontrada: {id}");

        _context.SocialChannelConfigs.Remove(config);
        await _context.SaveChangesAsync();
        _logger.LogInformation("Canal eliminado: {ChannelId}", id);
    }

    // ========== Obtención de credenciales (descifradas) ==========
    public async Task<(string ApiKey, string ApiSecret, string AccessToken, string AccessTokenSecret)?> GetDecryptedApiKeyCredentialsAsync(Guid id)
    {
        var config = await _context.SocialChannelConfigs.FindAsync(id);
        if (config == null || config.AuthMethod != AuthMethod.ApiKey)
            return null;

        if (config.ApiKey == null || config.ApiSecret == null || config.AccessTokenSecret == null)
            return null;

        return (
            _protector.Unprotect(config.ApiKey),
            _protector.Unprotect(config.ApiSecret),
            _protector.Unprotect(config.AccessToken),
            _protector.Unprotect(config.AccessTokenSecret)
        );
    }

    public async Task<(string AccessToken, string? RefreshToken)?> GetDecryptedOAuthCredentialsAsync(Guid id)
    {
        var config = await _context.SocialChannelConfigs.FindAsync(id);
        if (config == null)
            return null;

        return (
            _protector.Unprotect(config.AccessToken),
            config.RefreshToken != null ? _protector.Unprotect(config.RefreshToken) : null
        );
    }

    // ========== Verificación de conexión ==========
    public async Task<bool> TestConnectionAsync(Guid id)
    {
        var config = await _context.SocialChannelConfigs.FindAsync(id);
        if (config == null)
            return false;

        try
        {
            // TODO: Implementar verificación real según el tipo de red
            // Por ahora solo verificamos que los tokens existen
            if (config.AuthMethod == AuthMethod.ApiKey)
            {
                return config.ApiKey != null && config.ApiSecret != null && config.AccessTokenSecret != null;
            }
            else
            {
                return !string.IsNullOrEmpty(config.AccessToken) && config.AccessToken != "***PROTECTED***";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al verificar conexión para canal {ChannelId}", id);
            return false;
        }
    }

    // Método interno para obtener token desprotegido (para uso en publicación)
    public string GetDecryptedAccessToken(SocialChannelConfig config)
    {
        return _protector.Unprotect(config.AccessToken);
    }

    // ========== Configuración de medios ==========
    public async Task UpdateAllowMediaAsync(Guid channelId, bool allowMedia)
    {
        var channel = await _context.SocialChannelConfigs.FindAsync(channelId)
            ?? throw new InvalidOperationException($"Canal no encontrado: {channelId}");

        channel.AllowMedia = allowMedia;
        channel.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        _logger.LogInformation(
            "AllowMedia actualizado para canal {ChannelId}: {AllowMedia}",
            channelId, allowMedia);
    }
}
