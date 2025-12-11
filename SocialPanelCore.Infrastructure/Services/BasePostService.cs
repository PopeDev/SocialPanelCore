using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SocialPanelCore.Domain.Entities;
using SocialPanelCore.Domain.Enums;
using SocialPanelCore.Domain.Interfaces;
using SocialPanelCore.Infrastructure.Data;

namespace SocialPanelCore.Infrastructure.Services;

public class BasePostService : IBasePostService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<BasePostService> _logger;

    public BasePostService(ApplicationDbContext context, ILogger<BasePostService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<IEnumerable<BasePost>> GetPostsByAccountAsync(Guid accountId)
    {
        return await _context.BasePosts
            .AsNoTracking()
            .Include(p => p.TargetNetworks)
            .Include(p => p.CreatedByUser)
            .Where(p => p.AccountId == accountId)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<BasePost>> GetPostsPendingReviewAsync(Guid accountId)
    {
        return await _context.BasePosts
            .AsNoTracking()
            .Include(p => p.TargetNetworks)
            .Include(p => p.CreatedByUser)
            .Where(p => p.AccountId == accountId &&
                        p.State == BasePostState.Planificada &&
                        p.RequiresApproval == true)
            .OrderBy(p => p.ScheduledAtUtc)
            .ToListAsync();
    }

    public async Task<BasePost?> GetPostByIdAsync(Guid id)
    {
        return await _context.BasePosts
            .Include(p => p.TargetNetworks)
            .Include(p => p.Account)
            .Include(p => p.CreatedByUser)
            .FirstOrDefaultAsync(p => p.Id == id);
    }

    public async Task<BasePost> CreatePostAsync(
        Guid accountId,
        Guid? createdByUserId,
        string content,
        DateTime scheduledAtUtc,
        List<NetworkType> targetNetworks,
        string? title = null,
        BasePostState initialState = BasePostState.Borrador)
    {
        if (string.IsNullOrWhiteSpace(content))
            throw new ArgumentException("El contenido es obligatorio", nameof(content));

        if (!targetNetworks.Any())
            throw new ArgumentException("Debe seleccionar al menos una red social", nameof(targetNetworks));

        // Verificar que la cuenta existe
        var accountExists = await _context.Accounts.AnyAsync(a => a.Id == accountId);
        if (!accountExists)
            throw new InvalidOperationException($"Cuenta no encontrada: {accountId}");

        var post = new BasePost
        {
            Id = Guid.NewGuid(),
            AccountId = accountId,
            CreatedByUserId = createdByUserId,
            Title = title?.Trim(),
            Content = content.Trim(),
            ScheduledAtUtc = scheduledAtUtc.ToUniversalTime(),
            State = initialState,
            ContentType = DetermineContentType(content, targetNetworks),
            RequiresApproval = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        // Crear las redes objetivo
        post.TargetNetworks = targetNetworks.Select(nt => new PostTargetNetwork
        {
            Id = Guid.NewGuid(),
            BasePostId = post.Id,
            NetworkType = nt
        }).ToList();

        _context.BasePosts.Add(post);
        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "Post creado: {PostId} para cuenta {AccountId} con {NetworkCount} redes objetivo",
            post.Id, accountId, targetNetworks.Count);

        return post;
    }

    public async Task UpdatePostAsync(Guid id, string content, string? title, DateTime scheduledAtUtc)
    {
        var post = await _context.BasePosts.FindAsync(id)
            ?? throw new InvalidOperationException($"Post no encontrado: {id}");

        if (post.State == BasePostState.Publicada)
            throw new InvalidOperationException("No se puede editar un post ya publicado");

        post.Content = content.Trim();
        post.Title = title?.Trim();
        post.ScheduledAtUtc = scheduledAtUtc.ToUniversalTime();
        post.UpdatedAt = DateTime.UtcNow;

        // Si estaba adaptado, volver a estado pendiente de adaptación
        if (post.State == BasePostState.Adaptada)
        {
            post.State = BasePostState.AdaptacionPendiente;
        }

        await _context.SaveChangesAsync();
        _logger.LogInformation("Post actualizado: {PostId}", id);
    }

    public async Task DeletePostAsync(Guid id)
    {
        var post = await _context.BasePosts
            .Include(p => p.TargetNetworks)
            .Include(p => p.AdaptedVersions)
            .FirstOrDefaultAsync(p => p.Id == id)
            ?? throw new InvalidOperationException($"Post no encontrado: {id}");

        if (post.State == BasePostState.Publicada)
            throw new InvalidOperationException("No se puede eliminar un post ya publicado");

        _context.BasePosts.Remove(post);
        await _context.SaveChangesAsync();
        _logger.LogInformation("Post eliminado: {PostId}", id);
    }

    public async Task ApprovePostAsync(Guid postId, Guid approvedByUserId, string? notes)
    {
        var post = await _context.BasePosts.FindAsync(postId)
            ?? throw new InvalidOperationException($"Post no encontrado: {postId}");

        if (post.State != BasePostState.Planificada)
            throw new InvalidOperationException("Solo se pueden aprobar posts en estado Planificada");

        post.State = BasePostState.AdaptacionPendiente;
        post.ApprovedByUserId = approvedByUserId;
        post.ApprovedAt = DateTime.UtcNow;
        post.ApprovalNotes = notes;
        post.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        _logger.LogInformation("Post aprobado: {PostId} por usuario {UserId}", postId, approvedByUserId);
    }

    public async Task RejectPostAsync(Guid postId, Guid rejectedByUserId, string notes)
    {
        if (string.IsNullOrWhiteSpace(notes))
            throw new ArgumentException("Las notas son obligatorias al rechazar", nameof(notes));

        var post = await _context.BasePosts.FindAsync(postId)
            ?? throw new InvalidOperationException($"Post no encontrado: {postId}");

        if (post.State != BasePostState.Planificada)
            throw new InvalidOperationException("Solo se pueden rechazar posts en estado Planificada");

        post.State = BasePostState.Borrador;
        post.RejectedByUserId = rejectedByUserId;
        post.RejectedAt = DateTime.UtcNow;
        post.RejectionNotes = notes;
        post.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        _logger.LogInformation("Post rechazado: {PostId} por usuario {UserId}", postId, rejectedByUserId);
    }

    public async Task ChangeStateAsync(Guid postId, BasePostState newState)
    {
        var post = await _context.BasePosts.FindAsync(postId)
            ?? throw new InvalidOperationException($"Post no encontrado: {postId}");

        var oldState = post.State;
        post.State = newState;
        post.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        _logger.LogInformation("Post {PostId} cambió de estado: {OldState} -> {NewState}",
            postId, oldState, newState);
    }

    private static ContentType DetermineContentType(string content, List<NetworkType> networks)
    {
        if (networks.Any(n => n == NetworkType.TikTok || n == NetworkType.YouTube))
            return ContentType.Reel;

        if (networks.Any(n => n == NetworkType.Instagram) && content.Length < 100)
            return ContentType.Story;

        return ContentType.FeedPost;
    }
}
