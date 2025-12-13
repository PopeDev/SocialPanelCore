using SocialPanelCore.Domain.Entities;
using SocialPanelCore.Domain.Enums;

namespace SocialPanelCore.Domain.Interfaces;

/// <summary>
/// Almacén de estados OAuth para protección anti-CSRF y PKCE.
/// </summary>
public interface IOAuthStateStore
{
    /// <summary>
    /// Crea y almacena un nuevo estado OAuth.
    /// </summary>
    /// <param name="accountId">ID de la cuenta (tenant)</param>
    /// <param name="userId">ID del usuario que inicia el flujo</param>
    /// <param name="networkType">Red social</param>
    /// <param name="redirectUri">URI de callback</param>
    /// <param name="returnUrl">URL de retorno tras OAuth</param>
    /// <param name="requestedScopes">Scopes solicitados</param>
    /// <param name="usePkce">Si se debe generar code_verifier para PKCE</param>
    /// <returns>Estado OAuth creado con state y opcionalmente code_verifier</returns>
    Task<OAuthState> CreateStateAsync(
        Guid accountId,
        Guid userId,
        NetworkType networkType,
        string redirectUri,
        string? returnUrl = null,
        string? requestedScopes = null,
        bool usePkce = false);

    /// <summary>
    /// Valida y consume un estado OAuth (marcándolo como usado).
    /// </summary>
    /// <param name="state">Valor del state recibido en callback</param>
    /// <returns>Estado OAuth si es válido y no expirado, null si no</returns>
    Task<OAuthState?> ValidateAndConsumeAsync(string state);

    /// <summary>
    /// Obtiene un estado sin consumirlo (para inspección).
    /// </summary>
    Task<OAuthState?> GetByStateAsync(string state);

    /// <summary>
    /// Limpia estados expirados (para job de mantenimiento).
    /// </summary>
    Task<int> CleanupExpiredStatesAsync();
}
