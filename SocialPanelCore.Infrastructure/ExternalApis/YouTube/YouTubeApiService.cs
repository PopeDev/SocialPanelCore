using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.YouTube.v3;
using Google.Apis.YouTube.v3.Data;
using Microsoft.Extensions.Logging;

namespace SocialPanelCore.Infrastructure.ExternalApis.YouTube;

/// <summary>
/// Servicio para interactuar con YouTube Data API v3
/// </summary>
public class YouTubeApiService
{
    private readonly ILogger<YouTubeApiService> _logger;

    public YouTubeApiService(ILogger<YouTubeApiService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Crea un cliente de YouTube autenticado
    /// </summary>
    private YouTubeService CreateClient(string accessToken)
    {
        var credential = GoogleCredential.FromAccessToken(accessToken);

        return new YouTubeService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
            ApplicationName = "SocialPanelCore"
        });
    }

    /// <summary>
    /// Sube un video a YouTube
    /// </summary>
    public async Task<string> UploadVideoAsync(
        string accessToken,
        Stream videoStream,
        string title,
        string description,
        string[] tags,
        string privacyStatus = "public")
    {
        var youtubeService = CreateClient(accessToken);

        var video = new Video
        {
            Snippet = new VideoSnippet
            {
                Title = title,
                Description = description,
                Tags = tags,
                CategoryId = "22"  // People & Blogs
            },
            Status = new VideoStatus
            {
                PrivacyStatus = privacyStatus  // "public", "private", "unlisted"
            }
        };

        var videosInsertRequest = youtubeService.Videos.Insert(
            video,
            "snippet,status",
            videoStream,
            "video/*");

        videosInsertRequest.ProgressChanged += progress =>
        {
            _logger.LogDebug("Upload progress: {Status} - {BytesSent} bytes",
                progress.Status, progress.BytesSent);
        };

        videosInsertRequest.ResponseReceived += video =>
        {
            _logger.LogInformation("Video subido: {VideoId}", video.Id);
        };

        var uploadProgress = await videosInsertRequest.UploadAsync();

        if (uploadProgress.Status == Google.Apis.Upload.UploadStatus.Failed)
        {
            throw new Exception($"Error subiendo video: {uploadProgress.Exception?.Message}");
        }

        return videosInsertRequest.ResponseBody?.Id
            ?? throw new Exception("No se recibió ID del video");
    }

    /// <summary>
    /// Obtiene información de un video
    /// </summary>
    public async Task<Video?> GetVideoAsync(string accessToken, string videoId)
    {
        var youtubeService = CreateClient(accessToken);

        var request = youtubeService.Videos.List("snippet,status,statistics");
        request.Id = videoId;

        var response = await request.ExecuteAsync();
        return response.Items?.FirstOrDefault();
    }

    /// <summary>
    /// Elimina un video
    /// </summary>
    public async Task DeleteVideoAsync(string accessToken, string videoId)
    {
        var youtubeService = CreateClient(accessToken);
        await youtubeService.Videos.Delete(videoId).ExecuteAsync();
        _logger.LogInformation("Video eliminado: {VideoId}", videoId);
    }

    /// <summary>
    /// Actualiza información de un video
    /// </summary>
    public async Task<Video> UpdateVideoAsync(
        string accessToken,
        string videoId,
        string title,
        string description)
    {
        var youtubeService = CreateClient(accessToken);

        // Primero obtener el video actual
        var listRequest = youtubeService.Videos.List("snippet");
        listRequest.Id = videoId;
        var listResponse = await listRequest.ExecuteAsync();
        var video = listResponse.Items?.FirstOrDefault()
            ?? throw new Exception($"Video no encontrado: {videoId}");

        // Actualizar campos
        video.Snippet.Title = title;
        video.Snippet.Description = description;

        var updateRequest = youtubeService.Videos.Update(video, "snippet");
        return await updateRequest.ExecuteAsync();
    }

    /// <summary>
    /// Obtiene información del canal del usuario
    /// </summary>
    public async Task<Channel?> GetMyChannelAsync(string accessToken)
    {
        var youtubeService = CreateClient(accessToken);

        var request = youtubeService.Channels.List("snippet,statistics");
        request.Mine = true;

        var response = await request.ExecuteAsync();
        return response.Items?.FirstOrDefault();
    }
}
