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
        List<NetworkType> targetNetworks,
        string? title = null,
        BasePostState initialState = BasePostState.Borrador);
    Task UpdatePostAsync(Guid id, string content, string? title, DateTime scheduledAtUtc);
    Task DeletePostAsync(Guid id);
    Task ApprovePostAsync(Guid postId, Guid approvedByUserId, string? notes);
    Task RejectPostAsync(Guid postId, Guid rejectedByUserId, string notes);
    Task ChangeStateAsync(Guid postId, BasePostState newState);

    /// <summary>
    /// Actualiza la configuración de AI y medios para las redes de una publicación
    /// </summary>
    Task UpdateNetworkConfigsAsync(Guid postId, List<NetworkConfigUpdate> configs);
}

/// <summary>
/// DTO para actualizar la configuración de una red en una publicación
/// </summary>
public class NetworkConfigUpdate
{
    public Guid NetworkId { get; set; }
    public bool UseAiOptimization { get; set; }
    public bool IncludeMedia { get; set; }
}
