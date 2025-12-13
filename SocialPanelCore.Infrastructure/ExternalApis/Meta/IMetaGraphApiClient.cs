using Refit;

namespace SocialPanelCore.Infrastructure.ExternalApis.Meta;

/// <summary>
/// Cliente Refit para Meta Graph API (Facebook e Instagram)
/// Base URL: https://graph.facebook.com/v18.0
/// </summary>
public interface IMetaGraphApiClient
{
    #region Facebook

    /// <summary>
    /// Obtener páginas del usuario
    /// GET /me/accounts
    /// </summary>
    [Get("/me/accounts")]
    Task<MetaPagesResponse> GetUserPagesAsync(
        [Query("access_token")] string accessToken);

    /// <summary>
    /// Publicar en el feed de una página
    /// POST /{page-id}/feed
    /// </summary>
    [Post("/{pageId}/feed")]
    Task<MetaPostResponse> CreateFacebookPostAsync(
        string pageId,
        [Query("message")] string message,
        [Query("access_token")] string accessToken);

    /// <summary>
    /// Publicar con imagen en página
    /// POST /{page-id}/photos
    /// </summary>
    [Multipart]
    [Post("/{pageId}/photos")]
    Task<MetaPostResponse> CreateFacebookPhotoPostAsync(
        string pageId,
        [AliasAs("message")] string message,
        [AliasAs("url")] string imageUrl,
        [Query("access_token")] string accessToken);

    /// <summary>
    /// Eliminar un post de página
    /// DELETE /{post-id}
    /// </summary>
    [Delete("/{postId}")]
    Task<MetaDeleteResponse> DeleteFacebookPostAsync(
        string postId,
        [Query("access_token")] string accessToken);

    #endregion

    #region Instagram

    /// <summary>
    /// Obtener cuenta de Instagram Business vinculada a página
    /// GET /{page-id}?fields=instagram_business_account
    /// </summary>
    [Get("/{pageId}")]
    Task<MetaInstagramAccountResponse> GetInstagramAccountAsync(
        string pageId,
        [Query("fields")] string fields,
        [Query("access_token")] string accessToken);

    /// <summary>
    /// Crear contenedor de media para Instagram (Step 1)
    /// POST /{ig-user-id}/media
    /// Para imagen: image_url + caption
    /// Para video/reel: video_url + caption + media_type=REELS
    /// </summary>
    [Post("/{igUserId}/media")]
    Task<MetaMediaContainerResponse> CreateInstagramMediaContainerAsync(
        string igUserId,
        [Query("image_url")] string? imageUrl,
        [Query("video_url")] string? videoUrl,
        [Query("caption")] string caption,
        [Query("media_type")] string? mediaType,
        [Query("access_token")] string accessToken);

    /// <summary>
    /// Verificar estado del contenedor (Step 2 - opcional para videos)
    /// GET /{container-id}?fields=status_code
    /// </summary>
    [Get("/{containerId}")]
    Task<MetaContainerStatusResponse> GetContainerStatusAsync(
        string containerId,
        [Query("fields")] string fields,
        [Query("access_token")] string accessToken);

    /// <summary>
    /// Publicar media de Instagram (Step 3)
    /// POST /{ig-user-id}/media_publish
    /// </summary>
    [Post("/{igUserId}/media_publish")]
    Task<MetaPostResponse> PublishInstagramMediaAsync(
        string igUserId,
        [Query("creation_id")] string containerId,
        [Query("access_token")] string accessToken);

    /// <summary>
    /// Obtener media del perfil de Instagram
    /// GET /{ig-user-id}/media
    /// </summary>
    [Get("/{igUserId}/media")]
    Task<MetaMediaListResponse> GetInstagramMediaAsync(
        string igUserId,
        [Query("access_token")] string accessToken);

    #endregion
}

#region Response Models

public class MetaPagesResponse
{
    public List<MetaPage>? Data { get; set; }
}

public class MetaPage
{
    public string? Id { get; set; }
    public string? Name { get; set; }
    public string? AccessToken { get; set; }
}

public class MetaPostResponse
{
    public string? Id { get; set; }
}

public class MetaDeleteResponse
{
    public bool Success { get; set; }
}

public class MetaInstagramAccountResponse
{
    public MetaInstagramBusinessAccount? InstagramBusinessAccount { get; set; }
}

public class MetaInstagramBusinessAccount
{
    public string? Id { get; set; }
}

public class MetaMediaContainerResponse
{
    public string? Id { get; set; }
}

public class MetaContainerStatusResponse
{
    public string? StatusCode { get; set; }  // IN_PROGRESS, FINISHED, ERROR
}

public class MetaMediaListResponse
{
    public List<MetaMediaItem>? Data { get; set; }
}

public class MetaMediaItem
{
    public string? Id { get; set; }
    public string? MediaType { get; set; }
    public string? Caption { get; set; }
    public string? Permalink { get; set; }
}

#endregion
