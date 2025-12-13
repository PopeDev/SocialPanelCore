using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SocialPanelCore.Domain.Entities;
using SocialPanelCore.Domain.Enums;
using SocialPanelCore.Domain.Interfaces;
using SocialPanelCore.Infrastructure.Data;

namespace SocialPanelCore.Infrastructure.Services;

/// <summary>
/// Implementación del almacén de estados OAuth con soporte PKCE.
/// Almacena en base de datos para soportar escenarios multi-servidor.
/// </summary>
public class OAuthStateStore : IOAuthStateStore
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<OAuthStateStore> _logger;

    // Estado expira en 15 minutos (tiempo suficiente para completar OAuth)
    private static readonly TimeSpan StateLifetime = TimeSpan.FromMinutes(15);

    public OAuthStateStore(
        ApplicationDbContext context,
        ILogger<OAuthStateStore> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<OAuthState> CreateStateAsync(
        Guid accountId,
        Guid userId,
        NetworkType networkType,
        string redirectUri,
        string? returnUrl = null,
        string? requestedScopes = null,
        bool usePkce = false)
    {
        var now = DateTime.UtcNow;

        var oauthState = new OAuthState
        {
            Id = Guid.NewGuid(),
            State = GenerateSecureState(),
            AccountId = accountId,
            UserId = userId,
            NetworkType = networkType,
            RedirectUri = redirectUri,
            ReturnUrl = returnUrl,
            RequestedScopes = requestedScopes,
            CodeVerifier = usePkce ? GenerateCodeVerifier() : null,
            CreatedAt = now,
            ExpiresAt = now.Add(StateLifetime),
            IsConsumed = false
        };

        _context.OAuthStates.Add(oauthState);
        await _context.SaveChangesAsync();

        _logger.LogDebug(
            "Estado OAuth creado para {NetworkType}, cuenta {AccountId}, expira {ExpiresAt}",
            networkType, accountId, oauthState.ExpiresAt);

        return oauthState;
    }

    public async Task<OAuthState?> ValidateAndConsumeAsync(string state)
    {
        var oauthState = await _context.OAuthStates
            .FirstOrDefaultAsync(s => s.State == state);

        if (oauthState == null)
        {
            _logger.LogWarning("Estado OAuth no encontrado: {State}", MaskState(state));
            return null;
        }

        if (oauthState.IsConsumed)
        {
            _logger.LogWarning("Estado OAuth ya consumido: {State}", MaskState(state));
            return null;
        }

        if (oauthState.ExpiresAt < DateTime.UtcNow)
        {
            _logger.LogWarning("Estado OAuth expirado: {State}", MaskState(state));
            return null;
        }

        // Marcar como consumido
        oauthState.IsConsumed = true;
        await _context.SaveChangesAsync();

        _logger.LogDebug(
            "Estado OAuth consumido para {NetworkType}, cuenta {AccountId}",
            oauthState.NetworkType, oauthState.AccountId);

        return oauthState;
    }

    public async Task<OAuthState?> GetByStateAsync(string state)
    {
        return await _context.OAuthStates
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.State == state);
    }

    public async Task<int> CleanupExpiredStatesAsync()
    {
        var expiredStates = await _context.OAuthStates
            .Where(s => s.ExpiresAt < DateTime.UtcNow || s.IsConsumed)
            .ToListAsync();

        if (expiredStates.Count == 0)
            return 0;

        _context.OAuthStates.RemoveRange(expiredStates);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Limpiados {Count} estados OAuth expirados/consumidos", expiredStates.Count);

        return expiredStates.Count;
    }

    /// <summary>
    /// Genera un state seguro de 32 bytes codificado en Base64 URL-safe.
    /// </summary>
    private static string GenerateSecureState()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Base64UrlEncode(bytes);
    }

    /// <summary>
    /// Genera un code_verifier para PKCE según RFC 7636.
    /// Debe ser entre 43 y 128 caracteres, usando [A-Z] / [a-z] / [0-9] / "-" / "." / "_" / "~"
    /// </summary>
    private static string GenerateCodeVerifier()
    {
        // 32 bytes = 43 caracteres en Base64 URL-safe (cumple con mínimo de 43)
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Base64UrlEncode(bytes);
    }

    /// <summary>
    /// Genera el code_challenge a partir del code_verifier (SHA256).
    /// Este método es estático para poder usarlo desde OAuthService.
    /// </summary>
    public static string GenerateCodeChallenge(string codeVerifier)
    {
        using var sha256 = SHA256.Create();
        var bytes = System.Text.Encoding.ASCII.GetBytes(codeVerifier);
        var hash = sha256.ComputeHash(bytes);
        return Base64UrlEncode(hash);
    }

    /// <summary>
    /// Codifica bytes en Base64 URL-safe (sin padding, reemplaza + y /).
    /// </summary>
    private static string Base64UrlEncode(byte[] bytes)
    {
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    /// <summary>
    /// Enmascara el state para logs (muestra solo primeros 8 chars).
    /// </summary>
    private static string MaskState(string state)
    {
        if (string.IsNullOrEmpty(state) || state.Length <= 8)
            return "***";
        return state[..8] + "...";
    }
}
