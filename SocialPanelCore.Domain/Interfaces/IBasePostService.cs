using SocialPanelCore.Domain.Entities;
using SocialPanelCore.Domain.Enums;

namespace SocialPanelCore.Domain.Interfaces;

public interface IBasePostService
{
    Task<IEnumerable<BasePost>> GetPostsByAccountAsync(Guid accountId);
    Task<IEnumerable<BasePost>> GetPostsPendingReviewAsync(Guid accountId);
    Task<BasePost?> GetPostByIdAsync(Guid id);
    Task<BasePost> CreatePostAsync(
        Guid accountId,
        Guid? createdByUserId,
        string content,
        DateTime scheduledAtUtc,
        IEnumerable<NetworkType> targetNetworks,
        string? title = null,
        BasePostState state = BasePostState.Borrador);
    Task<BasePost> UpdatePostAsync(
        Guid id,
        string content,
        DateTime scheduledAtUtc,
        IEnumerable<NetworkType> targetNetworks,
        string? title = null);
    Task DeletePostAsync(Guid id);
    Task ApprovePostAsync(Guid postId, Guid reviewerId, string? notes);
    Task RejectPostAsync(Guid postId, Guid reviewerId, string notes);
}
