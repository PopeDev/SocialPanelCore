using Refit;

namespace SocialPanelCore.Infrastructure.ExternalApis.X;

/// <summary>
/// Cliente Refit para X (Twitter) API v2
/// Base URL: https://api.x.com
/// Docs: https://developer.x.com/en/docs/twitter-api
/// </summary>
public interface IXApiClient
{
    /// <summary>
    /// Crear un nuevo tweet
    /// POST /2/tweets
    /// </summary>
    [Post("/2/tweets")]
    Task<XTweetResponse> CreateTweetAsync(
        [Body] XTweetRequest request,
        [Header("Authorization")] string authorization);

    /// <summary>
    /// Eliminar un tweet
    /// DELETE /2/tweets/{id}
    /// </summary>
    [Delete("/2/tweets/{id}")]
    Task<XDeleteResponse> DeleteTweetAsync(
        string id,
        [Header("Authorization")] string authorization);

    /// <summary>
    /// Obtener información de un tweet
    /// GET /2/tweets/{id}
    /// </summary>
    [Get("/2/tweets/{id}")]
    Task<XTweetLookupResponse> GetTweetAsync(
        string id,
        [Header("Authorization")] string authorization,
        [Query] string? expansions = null,
        [Query("tweet.fields")] string? tweetFields = null);

    /// <summary>
    /// Obtener usuario autenticado
    /// GET /2/users/me
    /// </summary>
    [Get("/2/users/me")]
    Task<XUserResponse> GetCurrentUserAsync(
        [Header("Authorization")] string authorization);

    /// <summary>
    /// Subir media (imagen) - Step 1: INIT
    /// POST /2/media/upload (multipart)
    /// </summary>
    [Multipart]
    [Post("/2/media/upload")]
    Task<XMediaUploadInitResponse> InitMediaUploadAsync(
        [AliasAs("command")] string command,
        [AliasAs("total_bytes")] long totalBytes,
        [AliasAs("media_type")] string mediaType,
        [Header("Authorization")] string authorization);
}

#region Request/Response Models

public class XTweetRequest
{
    public string Text { get; set; } = string.Empty;
    public XTweetMedia? Media { get; set; }
}

public class XTweetMedia
{
    public List<string>? MediaIds { get; set; }
}

public class XTweetResponse
{
    public XTweetData? Data { get; set; }
}

public class XTweetData
{
    public string? Id { get; set; }
    public string? Text { get; set; }
}

public class XDeleteResponse
{
    public XDeleteData? Data { get; set; }
}

public class XDeleteData
{
    public bool Deleted { get; set; }
}

public class XTweetLookupResponse
{
    public XTweetData? Data { get; set; }
}

public class XUserResponse
{
    public XUserData? Data { get; set; }
}

public class XUserData
{
    public string? Id { get; set; }
    public string? Name { get; set; }
    public string? Username { get; set; }
}

public class XMediaUploadInitResponse
{
    public string? MediaIdString { get; set; }
    public long? ExpiresAfterSecs { get; set; }
}

#endregion
