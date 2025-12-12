namespace SocialPanelCore.Domain.Enums;

/// <summary>
/// Modo de publicación de un post
/// </summary>
public enum PublishMode
{
    /// <summary>
    /// Publicación programada para una fecha futura.
    /// Procesada por Hangfire en background.
    /// </summary>
    Scheduled = 0,

    /// <summary>
    /// Publicación inmediata.
    /// Se procesa de forma síncrona cuando el usuario confirma.
    /// </summary>
    Immediate = 1
}
