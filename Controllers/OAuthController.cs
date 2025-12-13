using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SocialPanelCore.Domain.Enums;
using SocialPanelCore.Domain.Interfaces;
using SocialPanelCore.Infrastructure.Services;

namespace SocialPanelCore.Controllers;

/// <summary>
/// Controlador para manejar flujos OAuth de conexión de redes sociales.
/// Endpoints:
///   GET /oauth/connect/{provider}?accountId=...&returnUrl=...
///   GET /oauth/callback/{provider}?code=...&state=...
/// </summary>
[ApiController]
[Route("oauth")]
public class OAuthController : ControllerBase
{
    private readonly IOAuthService _oauthService;
    private readonly IOAuthStateStore _stateStore;
    private readonly ISocialChannelConfigService _channelConfigService;
    private readonly ILogger<OAuthController> _logger;

    public OAuthController(
        IOAuthService oauthService,
        IOAuthStateStore stateStore,
        ISocialChannelConfigService channelConfigService,
        ILogger<OAuthController> logger)
    {
        _oauthService = oauthService;
        _stateStore = stateStore;
        _channelConfigService = channelConfigService;
        _logger = logger;
    }

    /// <summary>
    /// Inicia el flujo OAuth para conectar una red social.
    /// Redirige al usuario a la página de autorización del proveedor.
    /// </summary>
    /// <param name="provider">Proveedor: facebook, instagram, x</param>
    /// <param name="accountId">ID de la cuenta (tenant) a conectar</param>
    /// <param name="returnUrl">URL a la que volver tras completar OAuth</param>
    [HttpGet("connect/{provider}")]
    [Authorize]
    public async Task<IActionResult> Connect(
        string provider,
        [FromQuery] Guid accountId,
        [FromQuery] string? returnUrl = null)
    {
        // Validar proveedor
        if (!TryParseProvider(provider, out var networkType))
        {
            return BadRequest(new { error = "invalid_provider", message = $"Proveedor no soportado: {provider}" });
        }

        // Validar accountId
        if (accountId == Guid.Empty)
        {
            return BadRequest(new { error = "missing_account", message = "Se requiere accountId" });
        }

        // Obtener userId del usuario autenticado
        var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
        if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
        {
            return Unauthorized(new { error = "unauthorized", message = "Usuario no autenticado" });
        }

        // Construir redirect URI
        var redirectUri = BuildRedirectUri(provider);

        // Determinar si usar PKCE
        var usePkce = _oauthService.RequiresPkce(networkType);

        // Crear estado OAuth (incluye PKCE code_verifier si aplica)
        var oauthState = await _stateStore.CreateStateAsync(
            accountId: accountId,
            userId: userId,
            networkType: networkType,
            redirectUri: redirectUri,
            returnUrl: returnUrl ?? "/channels",
            requestedScopes: _oauthService.GetDefaultScopes(networkType),
            usePkce: usePkce);

        // Generar code_challenge si usa PKCE
        string? codeChallenge = null;
        if (usePkce && !string.IsNullOrEmpty(oauthState.CodeVerifier))
        {
            codeChallenge = OAuthStateStore.GenerateCodeChallenge(oauthState.CodeVerifier);
        }

        // Generar URL de autorización
        var authUrl = _oauthService.GetAuthorizationUrl(
            networkType,
            oauthState.State,
            redirectUri,
            codeChallenge);

        _logger.LogInformation(
            "Iniciando OAuth {Provider} para cuenta {AccountId}, usuario {UserId}",
            provider, accountId, userId);

        return Redirect(authUrl);
    }

    /// <summary>
    /// Callback OAuth - procesa el código de autorización y guarda los tokens.
    /// </summary>
    /// <param name="provider">Proveedor: facebook, instagram, x</param>
    /// <param name="code">Código de autorización</param>
    /// <param name="state">Estado anti-CSRF</param>
    /// <param name="error">Error si el usuario denegó acceso</param>
    /// <param name="error_description">Descripción del error</param>
    [HttpGet("callback/{provider}")]
    public async Task<IActionResult> Callback(
        string provider,
        [FromQuery] string? code,
        [FromQuery] string? state,
        [FromQuery] string? error,
        [FromQuery] string? error_description)
    {
        // Manejar errores del proveedor (usuario denegó acceso, etc.)
        if (!string.IsNullOrEmpty(error))
        {
            _logger.LogWarning(
                "OAuth {Provider} denegado: {Error} - {Description}",
                provider, error, error_description);

            return RedirectWithError("/channels", error, error_description ?? "Acceso denegado");
        }

        // Validar parámetros requeridos
        if (string.IsNullOrEmpty(code) || string.IsNullOrEmpty(state))
        {
            return RedirectWithError("/channels", "missing_params", "Faltan parámetros requeridos");
        }

        // Validar y consumir estado
        var oauthState = await _stateStore.ValidateAndConsumeAsync(state);
        if (oauthState == null)
        {
            _logger.LogWarning("Estado OAuth inválido o expirado: {State}", state[..Math.Min(8, state.Length)] + "...");
            return RedirectWithError("/channels", "invalid_state", "Estado inválido o expirado. Intenta de nuevo.");
        }

        // Validar que el provider coincide
        if (!TryParseProvider(provider, out var networkType) || networkType != oauthState.NetworkType)
        {
            return RedirectWithError(oauthState.ReturnUrl ?? "/channels", "provider_mismatch", "El proveedor no coincide");
        }

        try
        {
            // Intercambiar código por tokens
            var tokenResult = await _oauthService.ExchangeCodeForTokensAsync(
                networkType,
                code,
                oauthState.RedirectUri,
                oauthState.CodeVerifier); // null si no usa PKCE

            if (!tokenResult.Success)
            {
                _logger.LogWarning(
                    "Error intercambiando código OAuth {Provider}: {Error} - {Description}",
                    provider, tokenResult.Error, tokenResult.ErrorDescription);

                return RedirectWithError(
                    oauthState.ReturnUrl ?? "/channels",
                    tokenResult.Error ?? "exchange_error",
                    tokenResult.ErrorDescription ?? "Error al obtener tokens");
            }

            // Obtener información del usuario
            var userInfo = await _oauthService.GetUserInfoAsync(networkType, tokenResult.AccessToken!);

            // Verificar si ya existe una configuración para esta red
            var existingConfig = await _channelConfigService.GetChannelConfigByAccountAndNetworkAsync(
                oauthState.AccountId, networkType);

            if (existingConfig != null)
            {
                // Actualizar configuración existente
                await _channelConfigService.UpdateTokensAsync(
                    existingConfig.Id,
                    tokenResult.AccessToken!,
                    tokenResult.RefreshToken,
                    tokenResult.ExpiresAt);

                _logger.LogInformation(
                    "Conexión {Provider} actualizada para cuenta {AccountId}",
                    provider, oauthState.AccountId);
            }
            else
            {
                // Crear nueva configuración
                await _channelConfigService.CreateChannelConfigAsync(
                    oauthState.AccountId,
                    networkType,
                    tokenResult.AccessToken!,
                    tokenResult.RefreshToken,
                    tokenResult.ExpiresAt,
                    userInfo?.Username ?? userInfo?.DisplayName);

                _logger.LogInformation(
                    "Nueva conexión {Provider} creada para cuenta {AccountId}",
                    provider, oauthState.AccountId);
            }

            // Redirigir con éxito
            var returnUrl = oauthState.ReturnUrl ?? "/channels";
            var separator = returnUrl.Contains('?') ? "&" : "?";
            return Redirect($"{returnUrl}{separator}success=true&provider={provider}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error procesando callback OAuth {Provider}", provider);
            return RedirectWithError(
                oauthState.ReturnUrl ?? "/channels",
                "server_error",
                "Error interno del servidor");
        }
    }

    /// <summary>
    /// Desconecta una red social (revoca tokens si es posible y elimina la configuración).
    /// </summary>
    /// <param name="provider">Proveedor: facebook, instagram, x</param>
    /// <param name="accountId">ID de la cuenta</param>
    [HttpPost("disconnect/{provider}")]
    [Authorize]
    public async Task<IActionResult> Disconnect(string provider, [FromQuery] Guid accountId)
    {
        if (!TryParseProvider(provider, out var networkType))
        {
            return BadRequest(new { error = "invalid_provider", message = $"Proveedor no soportado: {provider}" });
        }

        var config = await _channelConfigService.GetChannelConfigByAccountAndNetworkAsync(accountId, networkType);
        if (config == null)
        {
            return NotFound(new { error = "not_found", message = "Conexión no encontrada" });
        }

        try
        {
            // Intentar revocar token si el proveedor lo soporta
            var credentials = await _channelConfigService.GetDecryptedOAuthCredentialsAsync(config.Id);
            if (credentials.HasValue)
            {
                await _oauthService.RevokeTokenAsync(networkType, credentials.Value.AccessToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo revocar token para {Provider}, continuando con eliminación", provider);
        }

        // Eliminar configuración
        await _channelConfigService.DeleteChannelAsync(config.Id);

        _logger.LogInformation(
            "Conexión {Provider} eliminada para cuenta {AccountId}",
            provider, accountId);

        return Ok(new { success = true, message = "Conexión eliminada" });
    }

    /// <summary>
    /// Reconecta una red social que necesita reautorización.
    /// Redirige al flujo OAuth normal.
    /// </summary>
    [HttpGet("reconnect/{provider}")]
    [Authorize]
    public Task<IActionResult> Reconnect(
        string provider,
        [FromQuery] Guid accountId,
        [FromQuery] string? returnUrl = null)
    {
        // Simplemente redirige al flujo de conexión normal
        return Connect(provider, accountId, returnUrl);
    }

    #region Helpers

    private static bool TryParseProvider(string provider, out NetworkType networkType)
    {
        networkType = provider.ToLowerInvariant() switch
        {
            "facebook" => NetworkType.Facebook,
            "instagram" => NetworkType.Instagram,
            "x" or "twitter" => NetworkType.X,
            _ => (NetworkType)(-1)
        };

        return (int)networkType >= 0;
    }

    private string BuildRedirectUri(string provider)
    {
        var request = HttpContext.Request;
        var scheme = request.Scheme;
        var host = request.Host.ToString();
        return $"{scheme}://{host}/oauth/callback/{provider.ToLowerInvariant()}";
    }

    private IActionResult RedirectWithError(string baseUrl, string error, string description)
    {
        var separator = baseUrl.Contains('?') ? "&" : "?";
        var errorEncoded = Uri.EscapeDataString(error);
        var descriptionEncoded = Uri.EscapeDataString(description);
        return Redirect($"{baseUrl}{separator}error={errorEncoded}&error_description={descriptionEncoded}");
    }

    #endregion
}
