using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SocialPanelCore.Domain.Entities;
using SocialPanelCore.Domain.Enums;
using SocialPanelCore.Domain.Interfaces;
using SocialPanelCore.Infrastructure.Data;

namespace SocialPanelCore.Infrastructure.Services;

/// <summary>
/// Servicio para publicación inmediata (síncrona).
/// Gestiona el flujo: Adaptar → Preview → Publicar
/// </summary>
public class ImmediatePublishService : IImmediatePublishService
{
    private readonly ApplicationDbContext _context;
    private readonly IAiContentService _aiContentService;
    private readonly ISocialPublisherService _publisherService;
    private readonly ILogger<ImmediatePublishService> _logger;

    public ImmediatePublishService(
        ApplicationDbContext context,
        IAiContentService aiContentService,
        ISocialPublisherService publisherService,
        ILogger<ImmediatePublishService> logger)
    {
        _context = context;
        _aiContentService = aiContentService;
        _publisherService = publisherService;
        _logger = logger;
    }

    /// <summary>
    /// Genera previews del contenido adaptado para cada red.
    /// No guarda en BD, solo devuelve los contenidos para revisión.
    /// </summary>
    public async Task<Dictionary<NetworkType, AdaptedContentPreview>> GeneratePreviewsAsync(Guid basePostId)
    {
        var post = await _context.BasePosts
            .Include(p => p.TargetNetworks)
            .Include(p => p.Account)
            .FirstOrDefaultAsync(p => p.Id == basePostId)
            ?? throw new InvalidOperationException($"Post no encontrado: {basePostId}");

        var previews = new Dictionary<NetworkType, AdaptedContentPreview>();

        foreach (var targetNetwork in post.TargetNetworks)
        {
            string adaptedContent;

            if (targetNetwork.UseAiOptimization)
            {
                // Generar con IA
                adaptedContent = await _aiContentService.AdaptContentAsync(
                    post.Content,
                    targetNetwork.NetworkType,
                    post.Account?.Name);
            }
            else
            {
                // Sin IA: contenido original
                adaptedContent = post.Content;
            }

            previews[targetNetwork.NetworkType] = new AdaptedContentPreview
            {
                NetworkType = targetNetwork.NetworkType,
                OriginalContent = post.Content,
                AdaptedContent = adaptedContent,
                CharacterCount = adaptedContent.Length,
                UsedAi = targetNetwork.UseAiOptimization,
                IncludeMedia = targetNetwork.IncludeMedia
            };
        }

        _logger.LogInformation(
            "Previews generados para post {PostId}: {Count} redes",
            basePostId, previews.Count);

        return previews;
    }

    /// <summary>
    /// Publica inmediatamente después de que el usuario confirme los previews.
    /// Guarda los AdaptedPosts y publica en las redes.
    /// </summary>
    public async Task<ImmediatePublishResult> PublishAfterPreviewAsync(
        Guid basePostId,
        Dictionary<NetworkType, string> editedContent)
    {
        var post = await _context.BasePosts
            .Include(p => p.TargetNetworks)
            .Include(p => p.Account)
                .ThenInclude(a => a.SocialChannels)
            .FirstOrDefaultAsync(p => p.Id == basePostId)
            ?? throw new InvalidOperationException($"Post no encontrado: {basePostId}");

        var result = new ImmediatePublishResult
        {
            BasePostId = basePostId,
            NetworkResults = new Dictionary<NetworkType, NetworkPublishResult>()
        };

        // Cambiar estado a AdaptacionPendiente temporalmente
        post.State = BasePostState.AdaptacionPendiente;
        await _context.SaveChangesAsync();

        var successCount = 0;
        var failCount = 0;

        foreach (var (network, content) in editedContent)
        {
            var targetNetwork = post.TargetNetworks.FirstOrDefault(tn => tn.NetworkType == network);
            if (targetNetwork == null) continue;

            try
            {
                // Crear AdaptedPost con el contenido (posiblemente editado por el usuario)
                var adaptedPost = new AdaptedPost
                {
                    Id = Guid.NewGuid(),
                    BasePostId = basePostId,
                    NetworkType = network,
                    AdaptedContent = content,
                    CharacterCount = content.Length,
                    State = AdaptedPostState.Ready,
                    CreatedAt = DateTime.UtcNow
                };

                _context.AdaptedPosts.Add(adaptedPost);
                await _context.SaveChangesAsync();

                // Publicar
                var publishResult = await _publisherService.PublishToNetworkAsync(adaptedPost.Id);

                result.NetworkResults[network] = new NetworkPublishResult
                {
                    Success = publishResult.Success,
                    ExternalId = publishResult.ExternalId,
                    ErrorMessage = publishResult.ErrorMessage
                };

                if (publishResult.Success)
                {
                    successCount++;
                    _logger.LogInformation("Publicado en {Network}: {ExternalId}", network, publishResult.ExternalId);
                }
                else
                {
                    failCount++;
                    _logger.LogWarning("Fallo en {Network}: {Error}", network, publishResult.ErrorMessage);
                }
            }
            catch (Exception ex)
            {
                failCount++;
                _logger.LogError(ex, "Error publicando en {Network}", network);
                result.NetworkResults[network] = new NetworkPublishResult
                {
                    Success = false,
                    ErrorMessage = ex.Message
                };
            }
        }

        // Actualizar estado final del post
        if (failCount == 0 && successCount > 0)
        {
            post.State = BasePostState.Publicada;
            post.PublishedAt = DateTime.UtcNow;
            result.OverallSuccess = true;
        }
        else if (successCount > 0)
        {
            post.State = BasePostState.ParcialmentePublicada;
            result.OverallSuccess = false;
            result.OverallMessage = $"Publicado en {successCount} de {successCount + failCount} redes";
        }
        else
        {
            post.State = BasePostState.Adaptada; // Volver a estado anterior
            result.OverallSuccess = false;
            result.OverallMessage = "No se pudo publicar en ninguna red";
        }

        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "Publicación inmediata completada para {PostId}: {Success}/{Total}",
            basePostId, successCount, successCount + failCount);

        return result;
    }

    /// <summary>
    /// Publica directamente sin preview (para posts sin IA)
    /// </summary>
    public async Task<ImmediatePublishResult> PublishDirectlyAsync(Guid basePostId)
    {
        var post = await _context.BasePosts
            .Include(p => p.TargetNetworks)
            .FirstOrDefaultAsync(p => p.Id == basePostId)
            ?? throw new InvalidOperationException($"Post no encontrado: {basePostId}");

        // Crear diccionario con contenido original para cada red
        var contentByNetwork = post.TargetNetworks.ToDictionary(
            tn => tn.NetworkType,
            tn => post.Content
        );

        return await PublishAfterPreviewAsync(basePostId, contentByNetwork);
    }
}
