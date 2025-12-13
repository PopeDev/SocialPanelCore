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
    /// Tamaño máximo de archivo en bytes.
    /// Por defecto: 10MB (10485760 bytes)
    /// </summary>
    public long MaxFileSizeBytes { get; set; } = 10 * 1024 * 1024;

    /// <summary>
    /// Extensiones de archivo permitidas (con punto).
    /// Por defecto: .jpg, .jpeg, .png
    /// </summary>
    public string[] AllowedExtensions { get; set; } = new[] { ".jpg", ".jpeg", ".png" };
}
