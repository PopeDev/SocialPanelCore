using SocialPanelCore.Domain.Enums;

namespace SocialPanelCore.Domain.Entities;

public class PostTargetNetwork
{
    public Guid Id { get; set; }
    public Guid BasePostId { get; set; }
    public NetworkType NetworkType { get; set; }

    // ========== CONFIGURACIÓN POR RED ==========

    /// <summary>
    /// Indica si esta red específica debe usar optimización por IA.
    /// Puede ser diferente al valor global de BasePost.AiOptimizationEnabled.
    /// </summary>
    public bool UseAiOptimization { get; set; } = true;

    /// <summary>
    /// Indica si esta red debe incluir los medios (imágenes) de la publicación.
    /// Solo tiene efecto si BasePost tiene medios asociados.
    /// </summary>
    public bool IncludeMedia { get; set; } = true;

    // ========== FIN CONFIGURACIÓN ==========

    // Navegación
    public virtual BasePost BasePost { get; set; } = null!;
}
