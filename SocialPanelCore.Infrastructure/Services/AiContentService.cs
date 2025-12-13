using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SocialPanelCore.Domain.Entities;
using SocialPanelCore.Domain.Enums;
using SocialPanelCore.Domain.Interfaces;
using SocialPanelCore.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace SocialPanelCore.Infrastructure.Services;

/// <summary>
/// Servicio de adaptación de contenido usando IA (OpenRouter API).
/// Genera versiones optimizadas del contenido para cada red social.
/// </summary>
public class AiContentService : IAiContentService
{
    private readonly ApplicationDbContext _context;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AiContentService> _logger;

    private readonly string? _apiKey;
    private readonly string _endpoint;
    private readonly string _modelId;
    private readonly double _temperature;
    private readonly int _maxTokens;

    public AiContentService(
        ApplicationDbContext context,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<AiContentService> logger)
    {
        _context = context;
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;

        // Cargar configuracion de OpenRouter (puede ser null si no esta configurado)
        _apiKey = _configuration["OpenRouter:ApiKey"];
        _endpoint = _configuration["OpenRouter:ApiEndpoint"] ?? "https://openrouter.ai/api/v1/chat/completions";
        _modelId = _configuration["OpenRouter:ModelId"] ?? "anthropic/claude-3-haiku";
        _temperature = double.Parse(_configuration["OpenRouter:Temperature"] ?? "0.7");
        _maxTokens = int.Parse(_configuration["OpenRouter:MaxTokens"] ?? "500");
    }

    /// <summary>
    /// Adapta el contenido original para una red social específica usando IA
    /// </summary>
    public async Task<string> AdaptContentAsync(
        string originalContent,
        NetworkType network,
        string? accountContext = null)
    {
        // Si no hay API key configurada, usar fallback
        if (string.IsNullOrEmpty(_apiKey))
        {
            _logger.LogWarning("OpenRouter API key no configurada. Usando adaptación básica.");
            return ApplyBasicRules(originalContent, network);
        }

        var prompt = BuildPrompt(originalContent, network, accountContext);

        try
        {
            var response = await CallOpenRouterAsync(prompt);
            _logger.LogInformation(
                "Contenido adaptado para {Network}: {Length} caracteres",
                network, response.Length);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adaptando contenido para {Network}", network);
            // Fallback: devolver contenido original con truncado básico
            return ApplyBasicRules(originalContent, network);
        }
    }

    /// <summary>
    /// Adapta el contenido para múltiples redes en paralelo
    /// </summary>
    public async Task<Dictionary<NetworkType, string>> AdaptContentForNetworksAsync(
        string originalContent,
        List<NetworkType> networks)
    {
        var results = new Dictionary<NetworkType, string>();
        var tasks = networks.Select(async network =>
        {
            var adapted = await AdaptContentAsync(originalContent, network);
            return (Network: network, Content: adapted);
        });

        var completedTasks = await Task.WhenAll(tasks);

        foreach (var result in completedTasks)
        {
            results[result.Network] = result.Content;
        }

        return results;
    }

    /// <summary>
    /// Crea un AdaptedPost para una red específica
    /// </summary>
    public async Task<AdaptedPost> CreateAdaptedPostAsync(
        Guid basePostId,
        NetworkType network,
        bool useAi = true)
    {
        var basePost = await _context.BasePosts
            .Include(p => p.Account)
            .FirstOrDefaultAsync(p => p.Id == basePostId)
            ?? throw new InvalidOperationException($"Post no encontrado: {basePostId}");

        string adaptedContent;

        if (useAi)
        {
            adaptedContent = await AdaptContentAsync(
                basePost.Content,
                network,
                basePost.Account?.Name);
        }
        else
        {
            // Sin IA: usar contenido original (con reglas básicas de truncado si es necesario)
            adaptedContent = ApplyBasicRules(basePost.Content, network);
        }

        var adaptedPost = new AdaptedPost
        {
            Id = Guid.NewGuid(),
            BasePostId = basePostId,
            NetworkType = network,
            AdaptedContent = adaptedContent,
            CharacterCount = adaptedContent.Length,
            State = AdaptedPostState.Ready,
            CreatedAt = DateTime.UtcNow
        };

        _context.AdaptedPosts.Add(adaptedPost);
        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "AdaptedPost creado: {Id} para {Network} (AI: {UseAi})",
            adaptedPost.Id, network, useAi);

        return adaptedPost;
    }

    #region Private Methods

    /// <summary>
    /// Construye el prompt para la IA según la red social
    /// </summary>
    private string BuildPrompt(string content, NetworkType network, string? accountContext)
    {
        var networkGuidelines = network switch
        {
            NetworkType.X => @"
                - Máximo 280 caracteres
                - Tono conversacional y directo
                - Usa hashtags relevantes (2-3 máximo)
                - Incluye call-to-action si es apropiado
                - Emojis con moderación",

            NetworkType.Instagram => @"
                - Máximo 2200 caracteres (ideal 125-150 para feed)
                - Tono visual y emocional
                - Hashtags al final (5-10 relevantes)
                - Emojis para dar vida al texto
                - Incluye call-to-action",

            NetworkType.Facebook => @"
                - Máximo 500 caracteres ideal
                - Tono cercano y comunitario
                - Preguntas para generar engagement
                - Hashtags mínimos (1-2)
                - Emojis moderados",

            NetworkType.LinkedIn => @"
                - Máximo 700 caracteres ideal
                - Tono profesional pero accesible
                - Estructura clara con párrafos cortos
                - Hashtags profesionales (3-5)
                - Sin emojis excesivos",

            NetworkType.TikTok => @"
                - Máximo 150 caracteres para caption
                - Tono juvenil y dinámico
                - Hashtags trending (3-5)
                - Emojis y slang apropiado
                - Call-to-action tipo 'comenta', 'comparte'",

            NetworkType.YouTube => @"
                - Descripción completa pero concisa
                - Primeras 2 líneas son las más importantes
                - Incluye keywords relevantes
                - Timestamps si aplica
                - Call-to-action para suscripción",

            _ => "- Adapta el contenido de forma natural"
        };

        var contextInfo = !string.IsNullOrEmpty(accountContext)
            ? $"La marca/negocio es: {accountContext}"
            : "";

        return $@"Eres un experto en marketing digital y redes sociales.
Tu tarea es adaptar el siguiente contenido para {network}.

{contextInfo}

DIRECTRICES PARA {network.ToString().ToUpper()}:
{networkGuidelines}

CONTENIDO ORIGINAL:
{content}

INSTRUCCIONES:
1. Mantén el mensaje principal intacto
2. Adapta el tono y formato según la red
3. Respeta los límites de caracteres
4. Optimiza para engagement
5. NO añadas información que no esté en el original
6. Responde SOLO con el contenido adaptado, sin explicaciones

CONTENIDO ADAPTADO PARA {network}:";
    }

    /// <summary>
    /// Llama a la API de OpenRouter
    /// </summary>
    private async Task<string> CallOpenRouterAsync(string prompt)
    {
        using var client = _httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.Add("Authorization", $"Bearer {_apiKey}");
        client.DefaultRequestHeaders.Add("HTTP-Referer", "https://socialpanelcore.local");

        var requestBody = new
        {
            model = _modelId,
            messages = new[]
            {
                new { role = "user", content = prompt }
            },
            temperature = _temperature,
            max_tokens = _maxTokens
        };

        var response = await client.PostAsJsonAsync(_endpoint, requestBody);
        var responseContent = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Error de OpenRouter: {Status} - {Content}",
                response.StatusCode, responseContent);
            throw new Exception($"Error de OpenRouter: {response.StatusCode}");
        }

        var result = JsonSerializer.Deserialize<OpenRouterResponse>(responseContent);
        return result?.Choices?.FirstOrDefault()?.Message?.Content?.Trim()
            ?? throw new Exception("Respuesta vacía de OpenRouter");
    }

    /// <summary>
    /// Aplica reglas básicas de adaptación (fallback sin IA)
    /// </summary>
    private static string ApplyBasicRules(string content, NetworkType network)
    {
        return network switch
        {
            NetworkType.X => TruncateWithEllipsis(content, 280),
            NetworkType.TikTok => TruncateWithEllipsis(content, 150),
            NetworkType.LinkedIn => content.Length > 700
                ? TruncateWithEllipsis(content, 700)
                : content,
            NetworkType.Instagram => content.Length > 2200
                ? TruncateWithEllipsis(content, 2200)
                : content,
            _ => content
        };
    }

    private static string TruncateWithEllipsis(string text, int maxLength)
    {
        if (text.Length <= maxLength) return text;
        return text.Substring(0, maxLength - 3) + "...";
    }

    #endregion

    #region Response Models

    private class OpenRouterResponse
    {
        [JsonPropertyName("choices")]
        public List<Choice>? Choices { get; set; }
    }

    private class Choice
    {
        [JsonPropertyName("message")]
        public Message? Message { get; set; }
    }

    private class Message
    {
        [JsonPropertyName("content")]
        public string? Content { get; set; }
    }

    #endregion
}
