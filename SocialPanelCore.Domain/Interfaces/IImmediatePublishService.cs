using SocialPanelCore.Domain.Enums;

namespace SocialPanelCore.Domain.Interfaces;

/// <summary>
/// Servicio para publicación inmediata (síncrona)
/// </summary>
public interface IImmediatePublishService
{
    /// <summary>
    /// Genera previews del contenido adaptado sin guardar en BD
    /// </summary>
    Task<Dictionary<NetworkType, AdaptedContentPreview>> GeneratePreviewsAsync(Guid basePostId);

    /// <summary>
    /// Publica después de que el usuario confirme/edite los previews
    /// </summary>
    Task<ImmediatePublishResult> PublishAfterPreviewAsync(Guid basePostId, Dictionary<NetworkType, string> editedContent);

    /// <summary>
    /// Publica directamente sin preview (para posts sin IA)
    /// </summary>
    Task<ImmediatePublishResult> PublishDirectlyAsync(Guid basePostId);
}

#region DTOs

/// <summary>
/// Preview del contenido adaptado para una red
/// </summary>
public class AdaptedContentPreview
{
    public NetworkType NetworkType { get; set; }
    public string OriginalContent { get; set; } = string.Empty;
    public string AdaptedContent { get; set; } = string.Empty;
    public int CharacterCount { get; set; }
    public bool UsedAi { get; set; }
    public bool IncludeMedia { get; set; }
}

/// <summary>
/// Resultado de publicación inmediata
/// </summary>
public class ImmediatePublishResult
{
    public Guid BasePostId { get; set; }
    public bool OverallSuccess { get; set; }
    public string? OverallMessage { get; set; }
    public Dictionary<NetworkType, NetworkPublishResult> NetworkResults { get; set; } = new();
}

/// <summary>
/// Resultado de publicación en una red específica
/// </summary>
public class NetworkPublishResult
{
    public bool Success { get; set; }
    public string? ExternalId { get; set; }
    public string? ErrorMessage { get; set; }
}

#endregion
