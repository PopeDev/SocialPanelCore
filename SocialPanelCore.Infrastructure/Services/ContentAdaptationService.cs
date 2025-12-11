using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SocialPanelCore.Domain.Entities;
using SocialPanelCore.Domain.Enums;
using SocialPanelCore.Domain.Interfaces;
using SocialPanelCore.Infrastructure.Data;

namespace SocialPanelCore.Infrastructure.Services;

public class ContentAdaptationService : IContentAdaptationService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<ContentAdaptationService> _logger;

    public ContentAdaptationService(
        ApplicationDbContext context,
        ILogger<ContentAdaptationService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task AdaptPendingPostsAsync()
    {
        _logger.LogInformation("Iniciando adaptación de posts pendientes");

        var pendingPosts = await _context.BasePosts
            .Include(p => p.TargetNetworks)
            .Include(p => p.AdaptedVersions)
            .Where(p => p.State == BasePostState.AdaptacionPendiente)
            .Take(10)
            .ToListAsync();

        _logger.LogInformation("Encontrados {Count} posts pendientes de adaptación", pendingPosts.Count);

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
            .Select(tn => tn.NetworkType)
            .ToList();

        foreach (var network in networksToAdapt)
        {
            await AdaptPostForNetworkAsync(post.Id, network);
        }

        // Recargar el post para verificar las versiones adaptadas
        var updatedPost = await _context.BasePosts
            .Include(p => p.TargetNetworks)
            .Include(p => p.AdaptedVersions)
            .FirstAsync(p => p.Id == post.Id);

        // Si todas las redes están adaptadas, cambiar estado
        if (updatedPost.TargetNetworks.All(tn =>
            updatedPost.AdaptedVersions.Any(av => av.NetworkType == tn.NetworkType)))
        {
            updatedPost.State = BasePostState.Adaptada;
            await _context.SaveChangesAsync();
        }
    }

    public async Task<AdaptedPost> AdaptPostForNetworkAsync(Guid basePostId, NetworkType network)
    {
        var basePost = await _context.BasePosts
            .Include(p => p.Account)
            .FirstOrDefaultAsync(p => p.Id == basePostId)
            ?? throw new InvalidOperationException($"Post no encontrado: {basePostId}");

        _logger.LogInformation(
            "Adaptando post {PostId} para red {Network}",
            basePostId, network);

        // Adaptar contenido según la red
        var adaptedContent = await GenerateAdaptedContentAsync(basePost, network);

        var adaptedPost = new AdaptedPost
        {
            Id = Guid.NewGuid(),
            BasePostId = basePostId,
            NetworkType = network,
            AdaptedContent = adaptedContent,
            CharacterCount = adaptedContent.Length,
            State = AdaptedPostState.Ready,
            CreatedAt = DateTime.UtcNow
        };

        _context.AdaptedPosts.Add(adaptedPost);
        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "Post adaptado: {AdaptedPostId} para red {Network}",
            adaptedPost.Id, network);

        return adaptedPost;
    }

    private async Task<string> GenerateAdaptedContentAsync(BasePost post, NetworkType network)
    {
        // TODO: Integrar con servicio de IA real
        // Por ahora, adaptación básica basada en reglas

        var content = post.Content;

        // Adaptación básica (placeholder para IA)
        var adapted = network switch
        {
            NetworkType.X => TruncateWithEllipsis(content, 280),
            NetworkType.LinkedIn => $"{content}\n\n#profesional #negocios",
            NetworkType.Instagram => $"{content}\n\n#instagram #socialmedia",
            NetworkType.TikTok => content.Length > 150
                ? TruncateWithEllipsis(content, 150)
                : content,
            NetworkType.Facebook => content,
            NetworkType.YouTube => $"{post.Title}\n\n{content}",
            _ => content
        };

        // Simular latencia de API de IA
        await Task.Delay(100);

        return adapted;
    }

    private static string TruncateWithEllipsis(string text, int maxLength)
    {
        if (text.Length <= maxLength) return text;
        return text.Substring(0, maxLength - 3) + "...";
    }
}
