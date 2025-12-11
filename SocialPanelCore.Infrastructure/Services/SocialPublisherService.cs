using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SocialPanelCore.Domain.Entities;
using SocialPanelCore.Domain.Enums;
using SocialPanelCore.Domain.Interfaces;
using SocialPanelCore.Infrastructure.Data;

namespace SocialPanelCore.Infrastructure.Services;

public class SocialPublisherService : ISocialPublisherService
{
    private readonly ApplicationDbContext _context;
    private readonly SocialChannelConfigService _channelConfigService;
    private readonly ILogger<SocialPublisherService> _logger;

    public SocialPublisherService(
        ApplicationDbContext context,
        SocialChannelConfigService channelConfigService,
        ILogger<SocialPublisherService> logger)
    {
        _context = context;
        _channelConfigService = channelConfigService;
        _logger = logger;
    }

    public async Task PublishScheduledPostsAsync()
    {
        _logger.LogInformation("Iniciando publicación de posts programados");

        var now = DateTime.UtcNow;

        // Obtener posts adaptados listos para publicar
        var postsToPublish = await _context.BasePosts
            .Include(p => p.AdaptedVersions)
            .Include(p => p.Account)
                .ThenInclude(a => a.SocialChannels)
            .Where(p => p.State == BasePostState.Adaptada &&
                        p.ScheduledAtUtc <= now)
            .Take(20)
            .ToListAsync();

        _logger.LogInformation("Encontrados {Count} posts listos para publicar", postsToPublish.Count);

        foreach (var post in postsToPublish)
        {
            await PublishPostAsync(post);
        }
    }

    private async Task PublishPostAsync(BasePost post)
    {
        var successCount = 0;
        var failCount = 0;

        foreach (var adaptedPost in post.AdaptedVersions.Where(ap => ap.State == AdaptedPostState.Ready))
        {
            var result = await PublishToNetworkAsync(adaptedPost.Id);

            if (result.Success)
            {
                successCount++;
            }
            else
            {
                failCount++;
                _logger.LogWarning(
                    "Fallo publicando en {Network}: {Error}",
                    adaptedPost.NetworkType, result.ErrorMessage);
            }
        }

        // Actualizar estado del post base
        if (failCount == 0 && successCount > 0)
        {
            post.State = BasePostState.Publicada;
            post.PublishedAt = DateTime.UtcNow;
        }
        else if (successCount > 0)
        {
            post.State = BasePostState.ParcialmentePublicada;
        }

        await _context.SaveChangesAsync();
    }

    public async Task<PublishResult> PublishToNetworkAsync(Guid adaptedPostId)
    {
        var adaptedPost = await _context.AdaptedPosts
            .Include(ap => ap.BasePost)
                .ThenInclude(bp => bp.Account)
                    .ThenInclude(a => a.SocialChannels)
            .FirstOrDefaultAsync(ap => ap.Id == adaptedPostId)
            ?? throw new InvalidOperationException($"Post adaptado no encontrado: {adaptedPostId}");

        var channelConfig = adaptedPost.BasePost.Account.SocialChannels
            .FirstOrDefault(c => c.NetworkType == adaptedPost.NetworkType && c.IsEnabled);

        if (channelConfig == null)
        {
            return new PublishResult
            {
                Success = false,
                ErrorMessage = $"No hay canal configurado para {adaptedPost.NetworkType}"
            };
        }

        try
        {
            // Obtener token desencriptado
            var accessToken = _channelConfigService.GetDecryptedAccessToken(channelConfig);

            // Publicar según la red
            var externalId = adaptedPost.NetworkType switch
            {
                NetworkType.Facebook => await PublishToFacebookAsync(adaptedPost, accessToken),
                NetworkType.Instagram => await PublishToInstagramAsync(adaptedPost, accessToken),
                NetworkType.X => await PublishToXAsync(adaptedPost, accessToken),
                NetworkType.LinkedIn => await PublishToLinkedInAsync(adaptedPost, accessToken),
                NetworkType.TikTok => await PublishToTikTokAsync(adaptedPost, accessToken),
                NetworkType.YouTube => await PublishToYouTubeAsync(adaptedPost, accessToken),
                _ => throw new NotSupportedException($"Red no soportada: {adaptedPost.NetworkType}")
            };

            // Actualizar post adaptado
            adaptedPost.State = AdaptedPostState.Published;
            adaptedPost.PublishedAt = DateTime.UtcNow;
            adaptedPost.ExternalPostId = externalId;

            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Post publicado exitosamente en {Network}: {ExternalId}",
                adaptedPost.NetworkType, externalId);

            return new PublishResult { Success = true, ExternalId = externalId };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error publicando post {PostId} en {Network}",
                adaptedPostId, adaptedPost.NetworkType);

            adaptedPost.State = AdaptedPostState.Failed;
            adaptedPost.LastError = ex.Message;
            adaptedPost.RetryCount++;

            await _context.SaveChangesAsync();

            // Actualizar health status del canal
            await _channelConfigService.UpdateHealthStatusAsync(
                channelConfig.Id, HealthStatus.KO, ex.Message);

            return new PublishResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    public async Task RetryFailedPublicationsAsync()
    {
        var failedPosts = await _context.AdaptedPosts
            .Where(ap => ap.State == AdaptedPostState.Failed && ap.RetryCount < 3)
            .Take(10)
            .ToListAsync();

        foreach (var post in failedPosts)
        {
            await PublishToNetworkAsync(post.Id);
        }
    }

    // Métodos placeholder para cada red social
    // TODO: Implementar con SDKs reales de cada plataforma

    private Task<string> PublishToFacebookAsync(AdaptedPost post, string accessToken)
    {
        _logger.LogInformation("Publicando en Facebook (simulado)");
        return Task.FromResult($"fb_{Guid.NewGuid():N}");
    }

    private Task<string> PublishToInstagramAsync(AdaptedPost post, string accessToken)
    {
        _logger.LogInformation("Publicando en Instagram (simulado)");
        return Task.FromResult($"ig_{Guid.NewGuid():N}");
    }

    private Task<string> PublishToXAsync(AdaptedPost post, string accessToken)
    {
        _logger.LogInformation("Publicando en X (simulado)");
        return Task.FromResult($"x_{Guid.NewGuid():N}");
    }

    private Task<string> PublishToLinkedInAsync(AdaptedPost post, string accessToken)
    {
        _logger.LogInformation("Publicando en LinkedIn (simulado)");
        return Task.FromResult($"li_{Guid.NewGuid():N}");
    }

    private Task<string> PublishToTikTokAsync(AdaptedPost post, string accessToken)
    {
        _logger.LogInformation("Publicando en TikTok (simulado)");
        return Task.FromResult($"tt_{Guid.NewGuid():N}");
    }

    private Task<string> PublishToYouTubeAsync(AdaptedPost post, string accessToken)
    {
        _logger.LogInformation("Publicando en YouTube (simulado)");
        return Task.FromResult($"yt_{Guid.NewGuid():N}");
    }
}
