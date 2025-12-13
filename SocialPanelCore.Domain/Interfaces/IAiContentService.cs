using SocialPanelCore.Domain.Entities;
using SocialPanelCore.Domain.Enums;

namespace SocialPanelCore.Domain.Interfaces;

/// <summary>
/// Servicio para adaptación de contenido usando IA
/// </summary>
public interface IAiContentService
{
    /// <summary>
    /// Adapta el contenido original para una red social específica
    /// </summary>
    /// <param name="originalContent">Contenido original a adaptar</param>
    /// <param name="network">Red social destino</param>
    /// <param name="accountContext">Contexto opcional (nombre de marca, etc.)</param>
    /// <returns>Contenido adaptado</returns>
    Task<string> AdaptContentAsync(string originalContent, NetworkType network, string? accountContext = null);

    /// <summary>
    /// Adapta el contenido para múltiples redes en paralelo
    /// </summary>
    Task<Dictionary<NetworkType, string>> AdaptContentForNetworksAsync(string originalContent, List<NetworkType> networks);

    /// <summary>
    /// Crea un AdaptedPost para una red específica
    /// </summary>
    /// <param name="basePostId">ID del post base</param>
    /// <param name="network">Red social</param>
    /// <param name="useAi">Si true usa IA, si false usa contenido original</param>
    Task<AdaptedPost> CreateAdaptedPostAsync(Guid basePostId, NetworkType network, bool useAi = true);
}
