using Microsoft.AspNetCore.Mvc;
using SocialPanelCore.Domain.Enums;
using SocialPanelCore.Domain.Interfaces;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace SocialPanelCore.Controllers;

/// <summary>
/// Controlador para manejar solicitudes de eliminación de datos de usuarios de Facebook.
/// Requerido por Meta Graph API: https://developers.facebook.com/docs/development/create-an-app/app-dashboard/data-deletion-callback
/// </summary>
[ApiController]
[Route("data-deletion")]
public class DataDeletionController : ControllerBase
{
    private readonly ISocialChannelConfigService _channelConfigService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<DataDeletionController> _logger;

    public DataDeletionController(
        ISocialChannelConfigService channelConfigService,
        IConfiguration configuration,
        ILogger<DataDeletionController> logger)
    {
        _channelConfigService = channelConfigService;
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// Endpoint de callback de Facebook para solicitudes de eliminación de datos.
    /// Facebook envía un POST con signed_request cuando un usuario elimina su app.
    /// </summary>
    /// <remarks>
    /// Formato de signed_request: base64url(signature).base64url(payload)
    /// El payload contiene: { "user_id": "123456789", "algorithm": "HMAC-SHA256", "issued_at": 1234567890 }
    /// </remarks>
    [HttpPost("facebook")]
    public async Task<IActionResult> FacebookDataDeletion([FromForm(Name = "signed_request")] string signedRequest)
    {
        try
        {
            if (string.IsNullOrEmpty(signedRequest))
            {
                _logger.LogWarning("Received empty signed_request from Facebook");
                return BadRequest(new { error = "invalid_request", message = "signed_request is required" });
            }

            // Decodificar signed_request
            var parts = signedRequest.Split('.');
            if (parts.Length != 2)
            {
                _logger.LogWarning("Invalid signed_request format: {SignedRequest}", signedRequest);
                return BadRequest(new { error = "invalid_format", message = "Invalid signed_request format" });
            }

            var signature = parts[0];
            var payload = parts[1];

            // Verificar firma con App Secret
            var appSecret = _configuration["Facebook:AppSecret"];
            if (string.IsNullOrEmpty(appSecret))
            {
                _logger.LogError("Facebook:AppSecret not configured");
                return StatusCode(500, new { error = "configuration_error", message = "App secret not configured" });
            }

            if (!VerifySignature(payload, signature, appSecret))
            {
                _logger.LogWarning("Invalid signature for signed_request");
                return Unauthorized(new { error = "invalid_signature", message = "Signature verification failed" });
            }

            // Decodificar payload
            var decodedPayload = Base64UrlDecode(payload);
            var data = JsonSerializer.Deserialize<FacebookDataDeletionRequest>(decodedPayload);

            if (data == null || string.IsNullOrEmpty(data.UserId))
            {
                _logger.LogWarning("Invalid payload data: {Payload}", decodedPayload);
                return BadRequest(new { error = "invalid_payload", message = "Invalid payload data" });
            }

            _logger.LogInformation(
                "Received Facebook data deletion request for user {UserId}",
                data.UserId);

            // Buscar y eliminar todas las configuraciones de Facebook/Instagram del usuario
            var deletedCount = await _channelConfigService.DeleteByExternalUserIdAsync(
                data.UserId,
                new[] { NetworkType.Facebook, NetworkType.Instagram });

            _logger.LogInformation(
                "Deleted {Count} channel configurations for Facebook user {UserId}",
                deletedCount,
                data.UserId);

            // Generar confirmation_code (requerido por Facebook)
            var confirmationCode = GenerateConfirmationCode(data.UserId);

            // Generar URL de estado (Facebook la mostrará al usuario)
            var statusUrl = $"{Request.Scheme}://{Request.Host}/data-deletion/status/{confirmationCode}";

            return Ok(new
            {
                url = statusUrl,
                confirmation_code = confirmationCode
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing Facebook data deletion request");
            return StatusCode(500, new { error = "internal_error", message = ex.Message });
        }
    }

    /// <summary>
    /// Endpoint opcional para que usuarios consulten el estado de su solicitud de eliminación.
    /// </summary>
    [HttpGet("status/{confirmationCode}")]
    public IActionResult GetDeletionStatus(string confirmationCode)
    {
        _logger.LogInformation("Status check for confirmation code: {Code}", confirmationCode);

        return Ok(new
        {
            confirmation_code = confirmationCode,
            status = "completed",
            message = "Your data has been successfully deleted from SocialPanelCore.",
            processed_at = DateTime.UtcNow
        });
    }

    #region Private Methods

    /// <summary>
    /// Verifica la firma HMAC-SHA256 del signed_request de Facebook.
    /// </summary>
    private bool VerifySignature(string payload, string signature, string appSecret)
    {
        try
        {
            var expectedSignature = ComputeHmacSha256(payload, appSecret);
            return signature == expectedSignature;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verifying signature");
            return false;
        }
    }

    /// <summary>
    /// Calcula HMAC-SHA256 para el payload usando el App Secret.
    /// </summary>
    private string ComputeHmacSha256(string payload, string secret)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var payloadBytes = Encoding.UTF8.GetBytes(payload);
        var hashBytes = hmac.ComputeHash(payloadBytes);
        return Base64UrlEncode(hashBytes);
    }

    /// <summary>
    /// Decodifica base64url (usado por Facebook para signed_request).
    /// </summary>
    private string Base64UrlDecode(string base64Url)
    {
        // Reemplazar caracteres URL-safe
        var base64 = base64Url.Replace('-', '+').Replace('_', '/');

        // Agregar padding si es necesario
        switch (base64.Length % 4)
        {
            case 2: base64 += "=="; break;
            case 3: base64 += "="; break;
        }

        var bytes = Convert.FromBase64String(base64);
        return Encoding.UTF8.GetString(bytes);
    }

    /// <summary>
    /// Codifica a base64url (sin padding).
    /// </summary>
    private string Base64UrlEncode(byte[] bytes)
    {
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    /// <summary>
    /// Genera un código de confirmación único para la solicitud de eliminación.
    /// </summary>
    private string GenerateConfirmationCode(string userId)
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var data = $"{userId}:{timestamp}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(data));
        return Convert.ToHexString(hash).ToLowerInvariant()[..16];
    }

    #endregion

    #region DTOs

    private class FacebookDataDeletionRequest
    {
        [System.Text.Json.Serialization.JsonPropertyName("user_id")]
        public string UserId { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("algorithm")]
        public string Algorithm { get; set; } = string.Empty;

        [System.Text.Json.Serialization.JsonPropertyName("issued_at")]
        public long IssuedAt { get; set; }
    }

    #endregion
}
