namespace SocialPanelCore.Domain.Configuration;

/// <summary>
/// Configuración del almacenamiento de archivos
/// </summary>
public class StorageSettings
{
    /// <summary>
    /// Nombre de la sección en appsettings.json
    /// </summary>
    public const string SectionName = "Storage";

    /// <summary>
    /// Ruta base donde se almacenan los archivos subidos.
    /// Ejemplo: /var/www/socialpanel/uploads
    /// </summary>
    public string UploadsPath { get; set; } = string.Empty;

    /// <summary>
    /// Tamaño máximo de archivo de imagen en bytes.
    /// Por defecto: 10MB (10485760 bytes)
    /// </summary>
    public long MaxImageFileSizeBytes { get; set; } = 10 * 1024 * 1024;

    /// <summary>
    /// Tamaño máximo de archivo de video en bytes.
    /// Por defecto: 500MB (524288000 bytes) - necesario para YouTube/TikTok
    /// </summary>
    public long MaxVideoFileSizeBytes { get; set; } = 500 * 1024 * 1024;

    /// <summary>
    /// Tamaño máximo de archivo en bytes (retrocompatibilidad).
    /// Usa MaxImageFileSizeBytes o MaxVideoFileSizeBytes para mayor precisión.
    /// Por defecto: 10MB (10485760 bytes)
    /// </summary>
    public long MaxFileSizeBytes { get; set; } = 10 * 1024 * 1024;

    /// <summary>
    /// Extensiones de imagen permitidas (con punto).
    /// Por defecto: .jpg, .jpeg, .png, .gif, .webp
    /// </summary>
    public string[] AllowedImageExtensions { get; set; } = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };

    /// <summary>
    /// Extensiones de video permitidas (con punto).
    /// Por defecto: .mp4, .mov, .avi, .webm, .mkv
    /// Nota: YouTube y TikTok requieren video para publicar
    /// </summary>
    public string[] AllowedVideoExtensions { get; set; } = new[] { ".mp4", ".mov", ".avi", ".webm", ".mkv" };

    /// <summary>
    /// Extensiones de archivo permitidas (todas - retrocompatibilidad).
    /// Por defecto incluye imágenes y videos.
    /// </summary>
    public string[] AllowedExtensions { get; set; } = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp", ".mp4", ".mov", ".avi", ".webm", ".mkv" };

    /// <summary>
    /// Indica si está habilitada la subida de videos.
    /// Por defecto: true
    /// </summary>
    public bool AllowVideoUpload { get; set; } = true;
}
