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
