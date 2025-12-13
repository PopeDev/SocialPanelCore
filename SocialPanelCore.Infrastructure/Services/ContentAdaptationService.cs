using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SocialPanelCore.Domain.Entities;
using SocialPanelCore.Domain.Enums;
using SocialPanelCore.Domain.Interfaces;
using SocialPanelCore.Infrastructure.Data;

namespace SocialPanelCore.Infrastructure.Services;

/// <summary>
/// Servicio de adaptacion de contenido para Hangfire (trabajos en background).
/// Procesa posts pendientes y los adapta para cada red social usando el servicio de IA.
/// </summary>
public class ContentAdaptationService : IContentAdaptationService
{
    private readonly ApplicationDbContext _context;
    private readonly IAiContentService _aiContentService;
    private readonly ILogger<ContentAdaptationService> _logger;

    public ContentAdaptationService(
        ApplicationDbContext context,
        IAiContentService aiContentService,
        ILogger<ContentAdaptationService> logger)
    {
        _context = context;
        _aiContentService = aiContentService;
        _logger = logger;
    }

    /// <summary>
    /// Procesa posts pendientes de adaptacion (llamado por Hangfire)
    /// </summary>
    public async Task AdaptPendingPostsAsync()
    {
        _logger.LogInformation("Iniciando adaptacion de posts pendientes");

        var pendingPosts = await _context.BasePosts
            .Include(p => p.TargetNetworks)
            .Include(p => p.AdaptedVersions)
            .Where(p => p.State == BasePostState.AdaptacionPendiente)
            .Take(10)
            .ToListAsync();

        _logger.LogInformation("Encontrados {Count} posts pendientes de adaptacion", pendingPosts.Count);

        foreach (var post in pendingPosts)
        {
            try
            {
                await AdaptPostAsync(post);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adaptando post {PostId}", post.Id);
            }
        }
    }

    private async Task AdaptPostAsync(BasePost post)
    {
        var networksToAdapt = post.TargetNetworks
            .Where(tn => !post.AdaptedVersions.Any(av => av.NetworkType == tn.NetworkType))
            .ToList();

        foreach (var network in networksToAdapt)
        {
            try
            {
                // Usar el servicio de IA para crear el AdaptedPost
                await _aiContentService.CreateAdaptedPostAsync(
                    post.Id,
                    network.NetworkType,
                    network.UseAiOptimization  // Respetar configuracion por red
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adaptando post {PostId} para {Network}",
                    post.Id, network.NetworkType);
            }
        }

        // Verificar si todas las redes estan adaptadas
        await _context.Entry(post).ReloadAsync();
        await _context.Entry(post).Collection(p => p.AdaptedVersions).LoadAsync();
        await _context.Entry(post).Collection(p => p.TargetNetworks).LoadAsync();

        if (post.TargetNetworks.All(tn =>
            post.AdaptedVersions.Any(av => av.NetworkType == tn.NetworkType)))
        {
            post.State = BasePostState.Adaptada;
            await _context.SaveChangesAsync();
            _logger.LogInformation("Post {PostId} completamente adaptado", post.Id);
        }
    }

    /// <summary>
    /// Adapta un post para una red especifica (metodo legacy para compatibilidad)
    /// </summary>
    public async Task<AdaptedPost> AdaptPostForNetworkAsync(Guid basePostId, NetworkType network)
    {
        return await _aiContentService.CreateAdaptedPostAsync(basePostId, network, useAi: true);
    }
}
