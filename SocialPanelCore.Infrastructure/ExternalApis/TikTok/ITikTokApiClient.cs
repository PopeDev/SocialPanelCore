using Refit;

namespace SocialPanelCore.Infrastructure.ExternalApis.TikTok;

/// <summary>
/// Cliente Refit para TikTok Content Posting API
/// Base URL: https://open.tiktokapis.com
/// Docs: https://developers.tiktok.com/doc/content-posting-api-get-started
/// </summary>
public interface ITikTokApiClient
{
    /// <summary>
    /// Inicializar publicación de foto
    /// POST /v2/post/publish/content/init/
    /// </summary>
    [Post("/v2/post/publish/content/init/")]
    Task<TikTokInitResponse> InitPhotoPublishAsync(
        [Body] TikTokPhotoPublishRequest request,
        [Header("Authorization")] string authorization,
        [Header("Content-Type")] string contentType = "application/json; charset=UTF-8");

    /// <summary>
    /// Inicializar publicación de video (modo inbox/draft)
    /// POST /v2/post/publish/inbox/video/init/
    /// </summary>
    [Post("/v2/post/publish/inbox/video/init/")]
    Task<TikTokVideoInitResponse> InitVideoPublishAsync(
        [Body] TikTokVideoPublishRequest request,
        [Header("Authorization")] string authorization,
        [Header("Content-Type")] string contentType = "application/json; charset=UTF-8");

    /// <summary>
    /// Consultar estado de publicación
    /// POST /v2/post/publish/status/fetch/
    /// </summary>
    [Post("/v2/post/publish/status/fetch/")]
    Task<TikTokStatusResponse> GetPublishStatusAsync(
        [Body] TikTokStatusRequest request,
        [Header("Authorization")] string authorization,
        [Header("Content-Type")] string contentType = "application/json; charset=UTF-8");

    /// <summary>
    /// Obtener información del usuario
    /// GET /v2/user/info/
    /// </summary>
    [Get("/v2/user/info/")]
    Task<TikTokUserInfoResponse> GetUserInfoAsync(
        [Header("Authorization")] string authorization,
        [Query("fields")] string fields = "open_id,union_id,avatar_url,display_name");
}

#region Request/Response Models

public class TikTokPhotoPublishRequest
{
    public TikTokPostInfo PostInfo { get; set; } = new();
    public TikTokSourceInfo SourceInfo { get; set; } = new();
}

public class TikTokVideoPublishRequest
{
    public TikTokSourceInfo SourceInfo { get; set; } = new();
}

public class TikTokPostInfo
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool DisableDuet { get; set; } = false;
    public bool DisableComment { get; set; } = false;
    public bool DisableStitch { get; set; } = false;
    public string PrivacyLevel { get; set; } = "PUBLIC_TO_EVERYONE";  // o MUTUAL_FOLLOW_FRIENDS, SELF_ONLY
}

public class TikTokSourceInfo
{
    public string Source { get; set; } = "PULL_FROM_URL";  // o FILE_UPLOAD
    public string? PhotoCoverIndex { get; set; }
    public List<string>? PhotoImages { get; set; }  // URLs de imágenes
    public long? VideoSize { get; set; }  // Para videos
    public int? ChunkSize { get; set; }   // Para videos grandes
    public int? TotalChunkCount { get; set; }
}

public class TikTokInitResponse
{
    public TikTokInitData? Data { get; set; }
    public TikTokError? Error { get; set; }
}

public class TikTokInitData
{
    public string? PublishId { get; set; }
}

public class TikTokVideoInitResponse
{
    public TikTokVideoInitData? Data { get; set; }
    public TikTokError? Error { get; set; }
}

public class TikTokVideoInitData
{
    public string? PublishId { get; set; }
    public string? UploadUrl { get; set; }
}

public class TikTokStatusRequest
{
    public string PublishId { get; set; } = string.Empty;
}

public class TikTokStatusResponse
{
    public TikTokStatusData? Data { get; set; }
    public TikTokError? Error { get; set; }
}

public class TikTokStatusData
{
    public string? Status { get; set; }  // PROCESSING_UPLOAD, PROCESSING_DOWNLOAD, SEND_TO_USER_INBOX, PUBLISH_COMPLETE, FAILED
    public string? PubliclyAvailablePostId { get; set; }
    public List<string>? FailReason { get; set; }
}

public class TikTokUserInfoResponse
{
    public TikTokUserData? Data { get; set; }
    public TikTokError? Error { get; set; }
}

public class TikTokUserData
{
    public TikTokUser? User { get; set; }
}

public class TikTokUser
{
    public string? OpenId { get; set; }
    public string? UnionId { get; set; }
    public string? AvatarUrl { get; set; }
    public string? DisplayName { get; set; }
}

public class TikTokError
{
    public string? Code { get; set; }
    public string? Message { get; set; }
    public string? LogId { get; set; }
}

#endregion
