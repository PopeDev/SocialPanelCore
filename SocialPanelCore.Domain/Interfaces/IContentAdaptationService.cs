using SocialPanelCore.Domain.Entities;
using SocialPanelCore.Domain.Enums;

namespace SocialPanelCore.Domain.Interfaces;

public interface IContentAdaptationService
{
    Task AdaptPendingPostsAsync();
    Task<AdaptedPost> AdaptPostForNetworkAsync(Guid basePostId, NetworkType network);
}
