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
        }

        return channels;
    }

    public async Task<SocialChannelConfig?> GetChannelConfigAsync(Guid id)
    {
        return await _context.SocialChannelConfigs.FindAsync(id);
    }

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
            "Canal social configurado: {NetworkType} para cuenta {AccountId}",
            networkType, accountId);

        return config;
    }

    public async Task UpdateTokensAsync(Guid id, string accessToken, string? refreshToken, DateTime? tokenExpiresAt)
    {
        var config = await _context.SocialChannelConfigs.FindAsync(id)
            ?? throw new InvalidOperationException($"Configuración no encontrada: {id}");

        config.AccessToken = _protector.Protect(accessToken);
        config.RefreshToken = refreshToken != null ? _protector.Protect(refreshToken) : null;
        config.TokenExpiresAt = tokenExpiresAt;
        config.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        _logger.LogInformation("Tokens actualizados para canal {ChannelId}", id);
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

    // Método interno para obtener token desprotegido (para uso en publicación)
    public string GetDecryptedAccessToken(SocialChannelConfig config)
    {
        return _protector.Unprotect(config.AccessToken);
    }
}
