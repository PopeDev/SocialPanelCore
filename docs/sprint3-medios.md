# Sprint 3: Sistema de Medios (Almacenamiento de Archivos)

**Duración estimada:** 3-4 días
**Prerrequisitos:** Sprint 1 y Sprint 2 completados

---

## Objetivo del Sprint

Implementar el sistema completo de almacenamiento de medios (imágenes):
- Servicio de almacenamiento local
- Guardado de archivos durante creación de publicación
- Visualización de archivos en las páginas View y Edit
- Configuración de medios por red social

---

## Arquitectura de Almacenamiento

### Estructura de Carpetas

```
/var/www/socialpanel/uploads/          # Carpeta raíz (configurable)
└── {NombreCuenta}/                    # Ej: peluqueriaspaco
    └── {RedSocial}/                   # Ej: instagram
        └── {titulo}_{fecha}_{guid}.{ext}  # Ej: navidad_20251224_a1b2c3d4.jpg
```

### Ejemplo Real

```
/var/www/socialpanel/uploads/
├── peluqueriaspaco/
│   ├── instagram/
│   │   ├── navidad_20251224_a1b2c3d4.jpg
│   │   ├── navidad_20251224_b2c3d4e5.png
│   │   └── aniversario_20251215_c3d4e5f6.jpg
│   ├── facebook/
│   │   └── navidad_20251224_a1b2c3d4.jpg
│   └── x/
│       └── (vacío - X no permite medios en esta cuenta)
└── clinicadental/
    └── instagram/
        └── promocion_20251220_d4e5f6g7.jpg
```

---

## Tareas

### Tarea 3.1: Configurar Ruta de Uploads

**Archivo a modificar:** `appsettings.json`

Añadir la configuración de almacenamiento:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "..."
  },
  "Storage": {
    "UploadsPath": "/var/www/socialpanel/uploads",
    "MaxFileSizeBytes": 10485760,
    "AllowedExtensions": [ ".jpg", ".jpeg", ".png" ]
  },
  // ... resto de configuración
}
```

**Para desarrollo local en Windows, usar:**
```json
"Storage": {
  "UploadsPath": "C:\\socialpanel\\uploads",
  ...
}
```

---

### Tarea 3.2: Crear Clase de Configuración

**Archivo a crear:** `SocialPanelCore.Domain/Configuration/StorageSettings.cs`

```csharp
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
```

---

### Tarea 3.3: Crear Interfaz IMediaStorageService

**Archivo a crear:** `SocialPanelCore.Domain/Interfaces/IMediaStorageService.cs`

```csharp
using Microsoft.AspNetCore.Components.Forms;
using SocialPanelCore.Domain.Entities;
using SocialPanelCore.Domain.Enums;

namespace SocialPanelCore.Domain.Interfaces;

/// <summary>
/// Servicio para almacenamiento de archivos multimedia
/// </summary>
public interface IMediaStorageService
{
    /// <summary>
    /// Guarda un archivo multimedia asociado a una publicación.
    /// El archivo se almacena en: {UploadsPath}/{accountName}/{network}/{titulo}_{fecha}_{guid}.{ext}
    /// </summary>
    /// <param name="basePostId">ID de la publicación</param>
    /// <param name="file">Archivo a guardar (IBrowserFile de Blazor)</param>
    /// <param name="accountName">Nombre de la cuenta (para la carpeta)</param>
    /// <param name="network">Red social (para la subcarpeta)</param>
    /// <param name="postTitle">Título del post (para el nombre del archivo)</param>
    /// <param name="sortOrder">Orden del archivo (para múltiples imágenes)</param>
    /// <returns>Entidad PostMedia con los datos del archivo guardado</returns>
    Task<PostMedia> SaveMediaAsync(
        Guid basePostId,
        IBrowserFile file,
        string accountName,
        NetworkType network,
        string postTitle,
        int sortOrder = 0);

    /// <summary>
    /// Guarda múltiples archivos para una publicación.
    /// Versión batch de SaveMediaAsync.
    /// </summary>
    Task<IEnumerable<PostMedia>> SaveMediaBatchAsync(
        Guid basePostId,
        IEnumerable<IBrowserFile> files,
        string accountName,
        NetworkType network,
        string postTitle);

    /// <summary>
    /// Obtiene todos los medios de una publicación
    /// </summary>
    Task<IEnumerable<PostMedia>> GetMediaByPostIdAsync(Guid basePostId);

    /// <summary>
    /// Elimina un archivo multimedia específico (BD + archivo físico)
    /// </summary>
    Task DeleteMediaAsync(Guid mediaId);

    /// <summary>
    /// Elimina todos los medios de una publicación (BD + archivos físicos)
    /// </summary>
    Task DeleteAllMediaByPostIdAsync(Guid basePostId);

    /// <summary>
    /// Obtiene la URL pública para acceder a un archivo
    /// </summary>
    string GetMediaUrl(PostMedia media);

    /// <summary>
    /// Obtiene la ruta física completa de un archivo
    /// </summary>
    string GetPhysicalPath(PostMedia media);

    /// <summary>
    /// Valida si un archivo es válido (extensión, tamaño)
    /// </summary>
    /// <returns>Tupla (esValido, mensajeError)</returns>
    (bool IsValid, string? ErrorMessage) ValidateFile(IBrowserFile file);
}
```

---

### Tarea 3.4: Implementar MediaStorageService

**Archivo a crear:** `SocialPanelCore.Infrastructure/Services/MediaStorageService.cs`

```csharp
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
        if (!Directory.Exists(path))
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
```

---

### Tarea 3.5: Registrar el Servicio en Program.cs

**Archivo a modificar:** `Program.cs`

1. Añadir la configuración de Storage:

```csharp
// Después de builder.Services.AddDbContext(...)

// Configuración de almacenamiento
builder.Services.Configure<StorageSettings>(
    builder.Configuration.GetSection(StorageSettings.SectionName));
```

2. Registrar el servicio:

```csharp
// Junto con los otros servicios
builder.Services.AddScoped<IMediaStorageService, MediaStorageService>();
```

3. Añadir el using necesario al inicio del archivo:

```csharp
using SocialPanelCore.Domain.Configuration;
```

---

### Tarea 3.6: Configurar Static Files para Servir Uploads

**Archivo a modificar:** `Program.cs`

Después de `app.UseStaticFiles();`, añadir:

```csharp
// Servir archivos de uploads
var uploadsPath = builder.Configuration.GetSection("Storage:UploadsPath").Value;
if (!string.IsNullOrEmpty(uploadsPath) && Directory.Exists(uploadsPath))
{
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new PhysicalFileProvider(uploadsPath),
        RequestPath = "/uploads"
    });
}
```

Añadir el using:

```csharp
using Microsoft.Extensions.FileProviders;
```

---

### Tarea 3.7: Modificar New.razor para Guardar Medios

**Archivo a modificar:** `Components/Pages/Publications/New.razor`

1. Añadir inyección del servicio:

```razor
@inject IMediaStorageService MediaStorageService
```

2. Modificar el método `SaveAndSchedule` para guardar los archivos:

```csharp
private async Task SaveAndSchedule()
{
    await _form.Validate();
    if (!_formIsValid) return;

    if (!_selectedNetworks.Values.Any(v => v))
    {
        Snackbar.Add("Debe seleccionar al menos una red social", Severity.Warning);
        return;
    }

    if (_model.AccountId == Guid.Empty)
    {
        Snackbar.Add("Debe seleccionar una cuenta", Severity.Warning);
        return;
    }

    _processing = true;
    try
    {
        var selectedNetworks = _selectedNetworks.Where(x => x.Value).Select(x => x.Key).ToList();
        var userId = await GetCurrentUserIdAsync();

        // Determinar fecha de publicación
        DateTime scheduledDate;
        if (_model.ScheduleNow)
        {
            scheduledDate = DateTime.UtcNow;
        }
        else if (_model.ScheduledDate.HasValue && _model.ScheduledTime.HasValue)
        {
            scheduledDate = _model.ScheduledDate.Value.Date + _model.ScheduledTime.Value;
        }
        else
        {
            scheduledDate = DateTime.UtcNow;
        }

        // Crear publicación con estado Planificada
        var post = await BasePostService.CreatePostAsync(
            _model.AccountId,
            userId,
            _model.Content,
            scheduledDate,
            selectedNetworks,
            _model.Title,
            BasePostState.Planificada
        );

        // NUEVO: Guardar medios si hay archivos subidos
        if (_uploadedFiles.Any())
        {
            var account = _accounts.FirstOrDefault(a => a.Id == _model.AccountId);
            var accountName = account?.Name ?? "default";

            // Guardar para la primera red seleccionada (los medios son compartidos)
            var primaryNetwork = selectedNetworks.First();

            try
            {
                await MediaStorageService.SaveMediaBatchAsync(
                    post.Id,
                    _uploadedFiles,
                    accountName,
                    primaryNetwork,
                    _model.Title ?? "publicacion"
                );

                Snackbar.Add(
                    $"Publicación creada con {_uploadedFiles.Count} archivo(s) adjunto(s)",
                    Severity.Success);
            }
            catch (Exception ex)
            {
                Snackbar.Add(
                    $"Publicación creada pero hubo error con algunos archivos: {ex.Message}",
                    Severity.Warning);
            }
        }
        else
        {
            Snackbar.Add(
                _model.ScheduleNow
                    ? "Publicación creada. Las variantes se generarán automáticamente."
                    : "Publicación programada exitosamente",
                Severity.Success);
        }

        Navigation.NavigateTo("/publications");
    }
    catch (Exception ex)
    {
        Snackbar.Add($"Error: {ex.Message}", Severity.Error);
    }
    finally
    {
        _processing = false;
    }
}
```

3. Hacer lo mismo para `SaveAsDraft`:

```csharp
private async Task SaveAsDraft()
{
    await _form.Validate();
    if (!_formIsValid) return;

    var selectedNetworks = _selectedNetworks.Where(x => x.Value).Select(x => x.Key).ToList();
    if (!selectedNetworks.Any())
    {
        Snackbar.Add("Debe seleccionar al menos una red social", Severity.Warning);
        return;
    }

    if (_model.AccountId == Guid.Empty)
    {
        Snackbar.Add("Debe seleccionar una cuenta", Severity.Warning);
        return;
    }

    _processing = true;
    try
    {
        var userId = await GetCurrentUserIdAsync();
        var scheduledDate = DateTime.UtcNow.AddDays(1);

        var post = await BasePostService.CreatePostAsync(
            _model.AccountId,
            userId,
            _model.Content,
            scheduledDate,
            selectedNetworks,
            _model.Title,
            BasePostState.Borrador
        );

        // NUEVO: Guardar medios si hay archivos subidos
        if (_uploadedFiles.Any())
        {
            var account = _accounts.FirstOrDefault(a => a.Id == _model.AccountId);
            var accountName = account?.Name ?? "default";
            var primaryNetwork = selectedNetworks.First();

            try
            {
                await MediaStorageService.SaveMediaBatchAsync(
                    post.Id,
                    _uploadedFiles,
                    accountName,
                    primaryNetwork,
                    _model.Title ?? "publicacion"
                );
            }
            catch (Exception ex)
            {
                Snackbar.Add($"Error guardando algunos archivos: {ex.Message}", Severity.Warning);
            }
        }

        Snackbar.Add("Borrador guardado exitosamente", Severity.Success);
        Navigation.NavigateTo("/publications");
    }
    catch (Exception ex)
    {
        Snackbar.Add($"Error: {ex.Message}", Severity.Error);
    }
    finally
    {
        _processing = false;
    }
}
```

4. Añadir validación visual de archivos:

```csharp
private Task OnFilesSelected(InputFileChangeEventArgs e)
{
    _uploadedFiles.Clear();
    var errors = new List<string>();

    foreach (var file in e.GetMultipleFiles(10))
    {
        var (isValid, errorMessage) = MediaStorageService.ValidateFile(file);
        if (isValid)
        {
            _uploadedFiles.Add(file);
        }
        else
        {
            errors.Add($"{file.Name}: {errorMessage}");
        }
    }

    if (errors.Any())
    {
        Snackbar.Add(string.Join("\n", errors), Severity.Warning);
    }

    return Task.CompletedTask;
}
```

---

### Tarea 3.8: Añadir Configuración de AllowMedia en Redes Sociales

**Archivo a modificar:** `Components/Pages/SocialChannels/Index.razor`

Buscar la tabla donde se muestran los canales y añadir una columna para AllowMedia:

```razor
<!-- En la tabla de canales, añadir columna -->
<MudTh>Permitir Medios</MudTh>

<!-- En el RowTemplate, añadir celda -->
<MudTd DataLabel="Medios">
    <MudSwitch T="bool"
               Value="@context.AllowMedia"
               ValueChanged="@((bool val) => ToggleAllowMedia(context, val))"
               Color="Color.Info" />
</MudTd>
```

Añadir el método:

```csharp
private async Task ToggleAllowMedia(SocialChannelConfig channel, bool value)
{
    try
    {
        await SocialChannelConfigService.UpdateAllowMediaAsync(channel.Id, value);
        channel.AllowMedia = value;
        Snackbar.Add($"Configuración de medios actualizada para {channel.NetworkType}", Severity.Success);
    }
    catch (Exception ex)
    {
        Snackbar.Add($"Error: {ex.Message}", Severity.Error);
    }
}
```

---

### Tarea 3.9: Añadir Método UpdateAllowMediaAsync al Servicio

**Archivo a modificar:** `SocialPanelCore.Domain/Interfaces/ISocialChannelConfigService.cs`

Añadir:

```csharp
/// <summary>
/// Actualiza si una red social permite publicar medios
/// </summary>
Task UpdateAllowMediaAsync(Guid channelId, bool allowMedia);
```

**Archivo a modificar:** `SocialPanelCore.Infrastructure/Services/SocialChannelConfigService.cs`

Añadir implementación:

```csharp
public async Task UpdateAllowMediaAsync(Guid channelId, bool allowMedia)
{
    var channel = await _context.SocialChannelConfigs.FindAsync(channelId)
        ?? throw new InvalidOperationException($"Canal no encontrado: {channelId}");

    channel.AllowMedia = allowMedia;
    channel.UpdatedAt = DateTime.UtcNow;

    await _context.SaveChangesAsync();
    _logger.LogInformation(
        "AllowMedia actualizado para canal {ChannelId}: {AllowMedia}",
        channelId, allowMedia);
}
```

---

### Tarea 3.10: Crear Carpeta de Uploads

**Script para crear la estructura inicial (Linux):**

```bash
#!/bin/bash
# crear_estructura_uploads.sh

UPLOADS_PATH="/var/www/socialpanel/uploads"

# Crear carpeta raíz
sudo mkdir -p "$UPLOADS_PATH"

# Establecer permisos (el usuario de la app debe poder escribir)
sudo chown -R www-data:www-data "$UPLOADS_PATH"
sudo chmod -R 755 "$UPLOADS_PATH"

echo "Carpeta de uploads creada en: $UPLOADS_PATH"
```

**Para desarrollo local, crear manualmente:**

```bash
# Linux/Mac
mkdir -p /var/www/socialpanel/uploads

# Windows (PowerShell)
New-Item -ItemType Directory -Path "C:\socialpanel\uploads" -Force
```

---

## Criterios de Aceptación

- [ ] Los archivos se guardan en la estructura correcta: `{cuenta}/{red}/{titulo}_{fecha}_{guid}.{ext}`
- [ ] Los archivos se pueden visualizar en la página View
- [ ] Solo se aceptan archivos JPG, JPEG y PNG
- [ ] El tamaño máximo de archivo es configurable (default 10MB)
- [ ] Se puede configurar AllowMedia por red social
- [ ] Al eliminar una publicación, se eliminan sus archivos
- [ ] Los nombres de archivo se sanitizan correctamente (sin caracteres especiales)

---

## Pruebas Manuales

1. **Subir imagen válida:**
   - Crear nueva publicación
   - Subir archivo JPG de menos de 10MB
   - Verificar que se muestra en la lista de archivos
   - Guardar y verificar que aparece en la vista de publicación

2. **Subir archivo inválido:**
   - Intentar subir PDF o archivo mayor a 10MB
   - Verificar que se muestra mensaje de error

3. **Verificar estructura de carpetas:**
   - Subir imagen para cuenta "Test" en Instagram
   - Verificar que el archivo está en `/uploads/test/instagram/`

4. **Configurar AllowMedia:**
   - Ir a configuración de redes sociales
   - Desactivar AllowMedia para X
   - Crear publicación con imagen para X e Instagram
   - Verificar comportamiento

---

## Siguiente Sprint

Una vez completado este sprint, continúa con:
- **Sprint 4:** `docs/sprint4-ai-flujos.md` - AI Optimization y flujos de publicación
