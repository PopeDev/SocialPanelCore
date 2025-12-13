using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SocialPanelCore.Domain.Configuration;
using SocialPanelCore.Domain.Entities;
using SocialPanelCore.Domain.Enums;
using SocialPanelCore.Domain.Interfaces;
using SocialPanelCore.Infrastructure.Data;

namespace SocialPanelCore.Infrastructure.Services;

/// <summary>
/// Implementación del servicio de almacenamiento de medios.
/// Almacena archivos en el sistema de archivos local siguiendo la estructura:
/// {UploadsPath}/{accountName}/{network}/{titulo}_{fecha}_{guid}.{ext}
/// </summary>
public class MediaStorageService : IMediaStorageService
{
    private readonly ApplicationDbContext _context;
    private readonly StorageSettings _settings;
    private readonly ILogger<MediaStorageService> _logger;

    public MediaStorageService(
        ApplicationDbContext context,
        IOptions<StorageSettings> settings,
        ILogger<MediaStorageService> logger)
    {
        _context = context;
        _settings = settings.Value;
        _logger = logger;

        // Asegurar que existe la carpeta raíz
        EnsureDirectoryExists(_settings.UploadsPath);
    }

    public async Task<PostMedia> SaveMediaAsync(
        Guid basePostId,
        IBrowserFile file,
        string accountName,
        NetworkType network,
        string postTitle,
        int sortOrder = 0)
    {
        // Validar archivo
        var (isValid, errorMessage) = ValidateFile(file);
        if (!isValid)
        {
            throw new InvalidOperationException(errorMessage);
        }

        // Sanitizar nombres para rutas seguras
        var safeAccountName = SanitizeForPath(accountName);
        var safeNetworkName = network.ToString().ToLowerInvariant();
        var safeTitle = SanitizeForPath(postTitle);

        // Generar nombre de archivo único
        var extension = Path.GetExtension(file.Name).ToLowerInvariant();
        var dateStr = DateTime.UtcNow.ToString("yyyyMMdd");
        var uniqueId = Guid.NewGuid().ToString("N").Substring(0, 8);
        var storedFileName = $"{safeTitle}_{dateStr}_{uniqueId}{extension}";

        // Construir rutas
        var relativePath = Path.Combine(safeAccountName, safeNetworkName, storedFileName);
        var fullDirectory = Path.Combine(_settings.UploadsPath, safeAccountName, safeNetworkName);
        var fullPath = Path.Combine(_settings.UploadsPath, relativePath);

        // Asegurar que existe el directorio
        EnsureDirectoryExists(fullDirectory);

        // Guardar archivo físico
        try
        {
            await using var stream = file.OpenReadStream(maxAllowedSize: _settings.MaxFileSizeBytes);
            await using var fileStream = new FileStream(fullPath, FileMode.Create);
            await stream.CopyToAsync(fileStream);

            _logger.LogInformation(
                "Archivo guardado: {Path} ({Size} bytes)",
                fullPath, file.Size);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error guardando archivo: {Path}", fullPath);
            throw new InvalidOperationException($"Error guardando archivo: {ex.Message}", ex);
        }

        // Crear registro en BD
        var media = new PostMedia
        {
            Id = Guid.NewGuid(),
            BasePostId = basePostId,
            OriginalFileName = file.Name,
            StoredFileName = storedFileName,
            RelativePath = relativePath.Replace("\\", "/"), // Normalizar a Unix-style
            ContentType = file.ContentType ?? GetContentType(extension),
            FileSize = file.Size,
            SortOrder = sortOrder,
            CreatedAt = DateTime.UtcNow
        };

        _context.PostMedia.Add(media);
        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "Media creado: {MediaId} para post {PostId}",
            media.Id, basePostId);

        return media;
    }

    public async Task<IEnumerable<PostMedia>> SaveMediaBatchAsync(
        Guid basePostId,
        IEnumerable<IBrowserFile> files,
        string accountName,
        NetworkType network,
        string postTitle)
    {
        var result = new List<PostMedia>();
        var sortOrder = 0;

        foreach (var file in files)
        {
            try
            {
                var media = await SaveMediaAsync(
                    basePostId, file, accountName, network, postTitle, sortOrder);
                result.Add(media);
                sortOrder++;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Error guardando archivo {FileName}, continuando con el resto",
                    file.Name);
                // Continuar con los demás archivos
            }
        }

        return result;
    }

    public async Task<IEnumerable<PostMedia>> GetMediaByPostIdAsync(Guid basePostId)
    {
        return await _context.PostMedia
            .AsNoTracking()
            .Where(m => m.BasePostId == basePostId)
            .OrderBy(m => m.SortOrder)
            .ToListAsync();
    }

    public async Task DeleteMediaAsync(Guid mediaId)
    {
        var media = await _context.PostMedia.FindAsync(mediaId);
        if (media == null)
        {
            _logger.LogWarning("Intento de eliminar media inexistente: {MediaId}", mediaId);
            return;
        }

        // Eliminar archivo físico
        var fullPath = GetPhysicalPath(media);
        if (File.Exists(fullPath))
        {
            try
            {
                File.Delete(fullPath);
                _logger.LogInformation("Archivo eliminado: {Path}", fullPath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error eliminando archivo: {Path}", fullPath);
                // Continuar con eliminación de BD aunque falle el archivo
            }
        }

        // Eliminar registro de BD
        _context.PostMedia.Remove(media);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Media eliminado: {MediaId}", mediaId);
    }

    public async Task DeleteAllMediaByPostIdAsync(Guid basePostId)
    {
        var mediaList = await _context.PostMedia
            .Where(m => m.BasePostId == basePostId)
            .ToListAsync();

        foreach (var media in mediaList)
        {
            // Eliminar archivo físico
            var fullPath = GetPhysicalPath(media);
            if (File.Exists(fullPath))
            {
                try
                {
                    File.Delete(fullPath);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error eliminando archivo: {Path}", fullPath);
                }
            }
        }

        // Eliminar todos los registros de BD
        _context.PostMedia.RemoveRange(mediaList);
        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "Eliminados {Count} archivos para post {PostId}",
            mediaList.Count, basePostId);
    }

    public string GetMediaUrl(PostMedia media)
    {
        // URL relativa para servir desde StaticFiles
        return $"/uploads/{media.RelativePath}";
    }

    public string GetPhysicalPath(PostMedia media)
    {
        return Path.Combine(_settings.UploadsPath, media.RelativePath.Replace("/", Path.DirectorySeparatorChar.ToString()));
    }

    public (bool IsValid, string? ErrorMessage) ValidateFile(IBrowserFile file)
    {
        // Validar tamaño
        if (file.Size > _settings.MaxFileSizeBytes)
        {
            var maxMB = _settings.MaxFileSizeBytes / (1024 * 1024);
            return (false, $"El archivo excede el tamaño máximo permitido ({maxMB}MB)");
        }

        // Validar extensión
        var extension = Path.GetExtension(file.Name).ToLowerInvariant();
        if (!_settings.AllowedExtensions.Contains(extension))
        {
            var allowed = string.Join(", ", _settings.AllowedExtensions);
            return (false, $"Extensión no permitida. Extensiones válidas: {allowed}");
        }

        // Validar tipo MIME (básico)
        var contentType = file.ContentType?.ToLowerInvariant() ?? "";
        if (!contentType.StartsWith("image/"))
        {
            return (false, "Solo se permiten archivos de imagen");
        }

        return (true, null);
    }

    #region Helpers

    /// <summary>
    /// Sanitiza un string para usarlo en rutas de archivo.
    /// Reemplaza caracteres no válidos y espacios.
    /// </summary>
    private static string SanitizeForPath(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return "sin_nombre";

        // Convertir a minúsculas
        var result = input.ToLowerInvariant();

        // Reemplazar espacios por guiones bajos
        result = result.Replace(" ", "_");

        // Eliminar acentos (básico)
        result = result
            .Replace("á", "a").Replace("é", "e").Replace("í", "i")
            .Replace("ó", "o").Replace("ú", "u").Replace("ñ", "n");

        // Eliminar caracteres no válidos para rutas
        result = Regex.Replace(result, @"[^a-z0-9_\-]", "");

        // Truncar si es muy largo
        if (result.Length > 50)
            result = result.Substring(0, 50);

        // Asegurar que no está vacío
        if (string.IsNullOrEmpty(result))
            result = "archivo";

        return result;
    }

    /// <summary>
    /// Asegura que un directorio existe, creándolo si es necesario
    /// </summary>
    private static void EnsureDirectoryExists(string path)
    {
        if (!string.IsNullOrEmpty(path) && !Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
        }
    }

    /// <summary>
    /// Obtiene el Content-Type basado en la extensión
    /// </summary>
    private static string GetContentType(string extension)
    {
        return extension.ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            _ => "application/octet-stream"
        };
    }

    #endregion
}
