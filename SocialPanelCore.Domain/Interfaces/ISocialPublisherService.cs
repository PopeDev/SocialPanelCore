using SocialPanelCore.Domain.Entities;

namespace SocialPanelCore.Domain.Interfaces;

public interface ISocialPublisherService
{
    Task PublishScheduledPostsAsync();
    Task<PublishResult> PublishToNetworkAsync(Guid adaptedPostId);
    Task RetryFailedPublicationsAsync();
}

public class PublishResult
{
    public bool Success { get; set; }
    public string? ExternalId { get; set; }
    public string? ErrorMessage { get; set; }
}
