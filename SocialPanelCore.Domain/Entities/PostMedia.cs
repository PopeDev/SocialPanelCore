namespace SocialPanelCore.Domain.Entities;

/// <summary>
/// Representa un archivo multimedia (imagen) asociado a una publicación.
/// Los archivos se almacenan en el servidor local siguiendo la estructura:
/// /uploads/{NombreCuenta}/{RedSocial}/{Titulo}_{Fecha}_{Guid}.{extension}
/// </summary>
public class PostMedia
{
    /// <summary>
    /// Identificador único del medio
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// ID de la publicación base a la que pertenece este medio
    /// </summary>
    public Guid BasePostId { get; set; }

    /// <summary>
    /// Nombre original del archivo subido por el usuario
    /// Ejemplo: "foto_navidad.jpg"
    /// </summary>
    public string OriginalFileName { get; set; } = string.Empty;

    /// <summary>
    /// Nombre del archivo almacenado en el servidor (incluye GUID para evitar colisiones)
    /// Ejemplo: "publicacion_navidad_24122025_a1b2c3d4.jpg"
    /// </summary>
    public string StoredFileName { get; set; } = string.Empty;

    /// <summary>
    /// Ruta relativa del archivo desde la carpeta uploads
    /// Ejemplo: "peluqueriaspaco/instagram/publicacion_navidad_24122025_a1b2c3d4.jpg"
    /// </summary>
    public string RelativePath { get; set; } = string.Empty;

    /// <summary>
    /// Tipo MIME del archivo
    /// Valores permitidos: "image/jpeg", "image/png"
    /// </summary>
    public string ContentType { get; set; } = string.Empty;

    /// <summary>
    /// Tamaño del archivo en bytes
    /// </summary>
    public long FileSize { get; set; }

    /// <summary>
    /// Orden de la imagen en la publicación (para carruseles)
    /// Comienza en 0
    /// </summary>
    public int SortOrder { get; set; }

    /// <summary>
    /// Fecha de creación del registro
    /// </summary>
    public DateTime CreatedAt { get; set; }

    // ========== Navegación ==========

    /// <summary>
    /// Publicación base a la que pertenece este medio
    /// </summary>
    public virtual BasePost BasePost { get; set; } = null!;
}
