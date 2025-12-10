using Microsoft.EntityFrameworkCore;
using SocialPanelCore.Domain.Entities;
using SocialPanelCore.Domain.Enums;
using SocialPanelCore.Domain.Interfaces;
using SocialPanelCore.Infrastructure.Data;

namespace SocialPanelCore.Infrastructure.Services;

public class BasePostService : IBasePostService
{
    private readonly SocialPanelDbContext _context;

    public BasePostService(SocialPanelDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<BasePost>> GetPostsByAccountAsync(Guid accountId)
    {
        return await _context.BasePosts
            .Include(p => p.TargetNetworks)
            .Include(p => p.CreatedByUser)
            .Where(p => p.AccountId == accountId)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<BasePost>> GetPostsPendingReviewAsync(Guid accountId)
    {
        return await _context.BasePosts
            .Include(p => p.TargetNetworks)
            .Include(p => p.CreatedByUser)
            .Where(p => p.AccountId == accountId &&
                        (p.State == BasePostState.Planificada || p.State == BasePostState.AdaptacionPendiente))
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();
    }

    public async Task<BasePost?> GetPostByIdAsync(Guid id)
    {
        return await _context.BasePosts
            .Include(p => p.TargetNetworks)
            .Include(p => p.CreatedByUser)
            .FirstOrDefaultAsync(p => p.Id == id);
    }

    public async Task<BasePost> CreatePostAsync(
        Guid accountId,
        Guid? createdByUserId,
        string content,
        DateTime scheduledAtUtc,
        IEnumerable<NetworkType> targetNetworks,
        string? title = null,
        BasePostState state = BasePostState.Borrador)
    {
        var post = new BasePost
        {
            Id = Guid.NewGuid(),
            AccountId = accountId,
            CreatedByUserId = createdByUserId,
            Title = title,
            Content = content,
            ContentType = ContentType.FeedPost,
            State = state,
            ScheduledAtUtc = scheduledAtUtc,
            CreatedAt = DateTime.UtcNow
        };

        foreach (var network in targetNetworks)
        {
            post.TargetNetworks.Add(new TargetNetwork
            {
                Id = Guid.NewGuid(),
                BasePostId = post.Id,
                NetworkType = network
            });
        }

        _context.BasePosts.Add(post);
        await _context.SaveChangesAsync();

        return post;
    }

    public async Task<BasePost> UpdatePostAsync(
        Guid id,
        string content,
        DateTime scheduledAtUtc,
        IEnumerable<NetworkType> targetNetworks,
        string? title = null)
    {
        var post = await _context.BasePosts
            .Include(p => p.TargetNetworks)
            .FirstOrDefaultAsync(p => p.Id == id)
            ?? throw new InvalidOperationException($"Post with id {id} not found");

        post.Title = title;
        post.Content = content;
        post.ScheduledAtUtc = scheduledAtUtc;
        post.UpdatedAt = DateTime.UtcNow;

        // Update target networks
        _context.TargetNetworks.RemoveRange(post.TargetNetworks);
        post.TargetNetworks.Clear();

        foreach (var network in targetNetworks)
        {
            post.TargetNetworks.Add(new TargetNetwork
            {
                Id = Guid.NewGuid(),
                BasePostId = post.Id,
                NetworkType = network
            });
        }

        await _context.SaveChangesAsync();

        return post;
    }

    public async Task DeletePostAsync(Guid id)
    {
        var post = await _context.BasePosts.FindAsync(id)
            ?? throw new InvalidOperationException($"Post with id {id} not found");

        _context.BasePosts.Remove(post);
        await _context.SaveChangesAsync();
    }

    public async Task ApprovePostAsync(Guid postId, Guid reviewerId, string? notes)
    {
        var post = await _context.BasePosts.FindAsync(postId)
            ?? throw new InvalidOperationException($"Post with id {postId} not found");

        post.State = BasePostState.Planificada;
        post.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
    }

    public async Task RejectPostAsync(Guid postId, Guid reviewerId, string notes)
    {
        var post = await _context.BasePosts.FindAsync(postId)
            ?? throw new InvalidOperationException($"Post with id {postId} not found");

        post.State = BasePostState.Borrador;
        post.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
    }
}
