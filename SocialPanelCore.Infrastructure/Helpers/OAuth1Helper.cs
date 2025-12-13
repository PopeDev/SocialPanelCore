using System.Security.Cryptography;
using System.Text;

namespace SocialPanelCore.Infrastructure.Helpers;

/// <summary>
/// Helper para generar headers OAuth 1.0a (requerido por X/Twitter)
/// </summary>
public static class OAuth1Helper
{
    /// <summary>
    /// Genera el header Authorization para OAuth 1.0a
    /// </summary>
    public static string GenerateOAuth1Header(
        string method,
        string url,
        string consumerKey,
        string consumerSecret,
        string accessToken,
        string accessTokenSecret,
        Dictionary<string, string>? additionalParams = null)
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        var nonce = Convert.ToBase64String(Encoding.UTF8.GetBytes(Guid.NewGuid().ToString("N")));

        var oauthParams = new SortedDictionary<string, string>
        {
            ["oauth_consumer_key"] = consumerKey,
            ["oauth_nonce"] = nonce,
            ["oauth_signature_method"] = "HMAC-SHA1",
            ["oauth_timestamp"] = timestamp,
            ["oauth_token"] = accessToken,
            ["oauth_version"] = "1.0"
        };

        // Añadir parámetros adicionales si los hay
        if (additionalParams != null)
        {
            foreach (var param in additionalParams)
            {
                oauthParams[param.Key] = param.Value;
            }
        }

        // Crear base string
        var paramString = string.Join("&",
            oauthParams.Select(kvp =>
                $"{Uri.EscapeDataString(kvp.Key)}={Uri.EscapeDataString(kvp.Value)}"));

        var baseString = $"{method.ToUpper()}&{Uri.EscapeDataString(url)}&{Uri.EscapeDataString(paramString)}";

        // Crear signing key
        var signingKey = $"{Uri.EscapeDataString(consumerSecret)}&{Uri.EscapeDataString(accessTokenSecret)}";

        // Generar firma
        using var hmac = new HMACSHA1(Encoding.UTF8.GetBytes(signingKey));
        var signatureBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(baseString));
        var signature = Convert.ToBase64String(signatureBytes);

        oauthParams["oauth_signature"] = signature;

        // Crear header (solo parámetros oauth_*)
        var headerValue = string.Join(", ",
            oauthParams
                .Where(kvp => kvp.Key.StartsWith("oauth_"))
                .Select(kvp =>
                    $"{Uri.EscapeDataString(kvp.Key)}=\"{Uri.EscapeDataString(kvp.Value)}\""));

        return $"OAuth {headerValue}";
    }
}
