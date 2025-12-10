using Microsoft.EntityFrameworkCore;
using SocialPanelCore.Domain.Entities;
using SocialPanelCore.Domain.Enums;
using SocialPanelCore.Domain.Interfaces;
using SocialPanelCore.Infrastructure.Data;

namespace SocialPanelCore.Infrastructure.Services;

public class SocialChannelConfigService : ISocialChannelConfigService
{
    private readonly SocialPanelDbContext _context;

    public SocialChannelConfigService(SocialPanelDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<SocialChannelConfig>> GetChannelConfigsByAccountAsync(Guid accountId)
    {
        return await _context.SocialChannelConfigs
            .Where(c => c.AccountId == accountId)
            .OrderBy(c => c.NetworkType)
            .ToListAsync();
    }

    public async Task<SocialChannelConfig?> GetChannelConfigAsync(Guid id)
    {
        return await _context.SocialChannelConfigs.FindAsync(id);
    }

    public async Task<SocialChannelConfig> CreateChannelConfigAsync(
        Guid accountId,
        NetworkType networkType,
        string? handle,
        string? accessToken,
        string? refreshToken,
        DateTime? tokenExpiresAt)
    {
        var config = new SocialChannelConfig
        {
            Id = Guid.NewGuid(),
            AccountId = accountId,
            NetworkType = networkType,
            Handle = handle,
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            TokenExpiresAt = tokenExpiresAt,
            IsEnabled = true,
            HealthStatus = HealthStatus.OK,
            CreatedAt = DateTime.UtcNow
        };

        _context.SocialChannelConfigs.Add(config);
        await _context.SaveChangesAsync();

        return config;
    }

    public async Task EnableChannelAsync(Guid id)
    {
        var config = await _context.SocialChannelConfigs.FindAsync(id)
            ?? throw new InvalidOperationException($"Channel config with id {id} not found");

        config.IsEnabled = true;
        config.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
    }

    public async Task DisableChannelAsync(Guid id)
    {
        var config = await _context.SocialChannelConfigs.FindAsync(id)
            ?? throw new InvalidOperationException($"Channel config with id {id} not found");

        config.IsEnabled = false;
        config.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
    }

    public async Task UpdateTokensAsync(Guid id, string accessToken, string? refreshToken, DateTime? expiresAt)
    {
        var config = await _context.SocialChannelConfigs.FindAsync(id)
            ?? throw new InvalidOperationException($"Channel config with id {id} not found");

        config.AccessToken = accessToken;
        config.RefreshToken = refreshToken;
        config.TokenExpiresAt = expiresAt;
        config.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
    }
}
