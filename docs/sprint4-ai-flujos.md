# Sprint 4: AI Optimization y Flujos de Publicación

**Duración estimada:** 5-7 días
**Prerrequisitos:** Sprints 1, 2 y 3 completados

---

## Objetivo del Sprint

Implementar:
- Integración real con servicio de IA (OpenRouter)
- Flujo de publicación inmediata con preview
- Flujo de publicación programada (con y sin IA)
- Página de preview/edición de contenido adaptado

---

## Flujos de Publicación

### Diagrama de Estados Actualizado

```
                    ┌─────────────────────────────────────────┐
                    │              CREAR POST                 │
                    └─────────────────────────────────────────┘
                                      │
                         ┌────────────┴────────────┐
                         │                         │
                    INMEDIATA                 PROGRAMADA
                         │                         │
                         ▼                         ▼
            ┌────────────────────┐     ┌────────────────────┐
            │   ¿AI Enabled?     │     │   Guardar como     │
            └────────────────────┘     │   PLANIFICADA      │
                 │           │         └────────────────────┘
                SÍ          NO                   │
                 │           │         ┌────────┴────────┐
                 ▼           ▼         │                 │
    ┌──────────────┐  ┌──────────────┐ │  Hangfire       │
    │ Adaptar      │  │ Publicar     │ │  (cada 5 min)   │
    │ (síncrono)   │  │ directo      │ │                 │
    └──────────────┘  └──────────────┘ ▼                 ▼
           │                 │    ┌─────────┐      ┌─────────┐
           ▼                 │    │ Sin IA  │      │ Con IA  │
    ┌──────────────┐         │    │ →Pub.   │      │ →Adapt. │
    │ PREVIEW      │         │    └─────────┘      └─────────┘
    │ (editable)   │         │         │                │
    └──────────────┘         │         │                ▼
           │                 │         │     ┌──────────────┐
           ▼                 │         │     │ ADAPTADA     │
    ┌──────────────┐         │         │     └──────────────┘
    │ ¿Confirmar?  │         │         │           │
    └──────────────┘         │         │           │ Hangfire
       │        │            │         │           │ (cada 5m)
      SÍ       NO            │         │           ▼
       │        │            │         │     ┌──────────────┐
       │        ▼            │         └────►│  PUBLICADA   │◄───┐
       │    Cancelar         │               └──────────────┘    │
       │                     │                                   │
       └─────────────────────┴───────────────────────────────────┘
```

---

## Tareas

### Tarea 4.1: Crear Servicio de IA Real (OpenRouter)

**Archivo a crear:** `SocialPanelCore.Infrastructure/Services/AiContentService.cs`

```csharp
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

    private readonly string _apiKey;
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

        // Cargar configuración de OpenRouter
        _apiKey = _configuration["OpenRouter:ApiKey"] ?? throw new InvalidOperationException("OpenRouter:ApiKey no configurado");
        _endpoint = _configuration["OpenRouter:Endpoint"] ?? "https://openrouter.ai/api/v1/chat/completions";
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
```

---

### Tarea 4.2: Crear Interfaz IAiContentService

**Archivo a crear:** `SocialPanelCore.Domain/Interfaces/IAiContentService.cs`

```csharp
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
```

---

### Tarea 4.3: Crear Servicio de Publicación Inmediata

**Archivo a crear:** `SocialPanelCore.Infrastructure/Services/ImmediatePublishService.cs`

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SocialPanelCore.Domain.Entities;
using SocialPanelCore.Domain.Enums;
using SocialPanelCore.Domain.Interfaces;
using SocialPanelCore.Infrastructure.Data;

namespace SocialPanelCore.Infrastructure.Services;

/// <summary>
/// Servicio para publicación inmediata (síncrona).
/// Gestiona el flujo: Adaptar → Preview → Publicar
/// </summary>
public class ImmediatePublishService : IImmediatePublishService
{
    private readonly ApplicationDbContext _context;
    private readonly IAiContentService _aiContentService;
    private readonly ISocialPublisherService _publisherService;
    private readonly ILogger<ImmediatePublishService> _logger;

    public ImmediatePublishService(
        ApplicationDbContext context,
        IAiContentService aiContentService,
        ISocialPublisherService publisherService,
        ILogger<ImmediatePublishService> logger)
    {
        _context = context;
        _aiContentService = aiContentService;
        _publisherService = publisherService;
        _logger = logger;
    }

    /// <summary>
    /// Genera previews del contenido adaptado para cada red.
    /// No guarda en BD, solo devuelve los contenidos para revisión.
    /// </summary>
    public async Task<Dictionary<NetworkType, AdaptedContentPreview>> GeneratePreviewsAsync(Guid basePostId)
    {
        var post = await _context.BasePosts
            .Include(p => p.TargetNetworks)
            .Include(p => p.Account)
            .FirstOrDefaultAsync(p => p.Id == basePostId)
            ?? throw new InvalidOperationException($"Post no encontrado: {basePostId}");

        var previews = new Dictionary<NetworkType, AdaptedContentPreview>();

        foreach (var targetNetwork in post.TargetNetworks)
        {
            string adaptedContent;

            if (targetNetwork.UseAiOptimization)
            {
                // Generar con IA
                adaptedContent = await _aiContentService.AdaptContentAsync(
                    post.Content,
                    targetNetwork.NetworkType,
                    post.Account?.Name);
            }
            else
            {
                // Sin IA: contenido original
                adaptedContent = post.Content;
            }

            previews[targetNetwork.NetworkType] = new AdaptedContentPreview
            {
                NetworkType = targetNetwork.NetworkType,
                OriginalContent = post.Content,
                AdaptedContent = adaptedContent,
                CharacterCount = adaptedContent.Length,
                UsedAi = targetNetwork.UseAiOptimization,
                IncludeMedia = targetNetwork.IncludeMedia
            };
        }

        _logger.LogInformation(
            "Previews generados para post {PostId}: {Count} redes",
            basePostId, previews.Count);

        return previews;
    }

    /// <summary>
    /// Publica inmediatamente después de que el usuario confirme los previews.
    /// Guarda los AdaptedPosts y publica en las redes.
    /// </summary>
    public async Task<ImmediatePublishResult> PublishAfterPreviewAsync(
        Guid basePostId,
        Dictionary<NetworkType, string> editedContent)
    {
        var post = await _context.BasePosts
            .Include(p => p.TargetNetworks)
            .Include(p => p.Account)
                .ThenInclude(a => a.SocialChannels)
            .FirstOrDefaultAsync(p => p.Id == basePostId)
            ?? throw new InvalidOperationException($"Post no encontrado: {basePostId}");

        var result = new ImmediatePublishResult
        {
            BasePostId = basePostId,
            NetworkResults = new Dictionary<NetworkType, NetworkPublishResult>()
        };

        // Cambiar estado a AdaptacionPendiente temporalmente
        post.State = BasePostState.AdaptacionPendiente;
        await _context.SaveChangesAsync();

        var successCount = 0;
        var failCount = 0;

        foreach (var (network, content) in editedContent)
        {
            var targetNetwork = post.TargetNetworks.FirstOrDefault(tn => tn.NetworkType == network);
            if (targetNetwork == null) continue;

            try
            {
                // Crear AdaptedPost con el contenido (posiblemente editado por el usuario)
                var adaptedPost = new AdaptedPost
                {
                    Id = Guid.NewGuid(),
                    BasePostId = basePostId,
                    NetworkType = network,
                    AdaptedContent = content,
                    CharacterCount = content.Length,
                    State = AdaptedPostState.Ready,
                    CreatedAt = DateTime.UtcNow
                };

                _context.AdaptedPosts.Add(adaptedPost);
                await _context.SaveChangesAsync();

                // Publicar
                var publishResult = await _publisherService.PublishToNetworkAsync(adaptedPost.Id);

                result.NetworkResults[network] = new NetworkPublishResult
                {
                    Success = publishResult.Success,
                    ExternalId = publishResult.ExternalId,
                    ErrorMessage = publishResult.ErrorMessage
                };

                if (publishResult.Success)
                {
                    successCount++;
                    _logger.LogInformation("Publicado en {Network}: {ExternalId}", network, publishResult.ExternalId);
                }
                else
                {
                    failCount++;
                    _logger.LogWarning("Fallo en {Network}: {Error}", network, publishResult.ErrorMessage);
                }
            }
            catch (Exception ex)
            {
                failCount++;
                _logger.LogError(ex, "Error publicando en {Network}", network);
                result.NetworkResults[network] = new NetworkPublishResult
                {
                    Success = false,
                    ErrorMessage = ex.Message
                };
            }
        }

        // Actualizar estado final del post
        if (failCount == 0 && successCount > 0)
        {
            post.State = BasePostState.Publicada;
            post.PublishedAt = DateTime.UtcNow;
            result.OverallSuccess = true;
        }
        else if (successCount > 0)
        {
            post.State = BasePostState.ParcialmentePublicada;
            result.OverallSuccess = false;
            result.OverallMessage = $"Publicado en {successCount} de {successCount + failCount} redes";
        }
        else
        {
            post.State = BasePostState.Adaptada; // Volver a estado anterior
            result.OverallSuccess = false;
            result.OverallMessage = "No se pudo publicar en ninguna red";
        }

        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "Publicación inmediata completada para {PostId}: {Success}/{Total}",
            basePostId, successCount, successCount + failCount);

        return result;
    }

    /// <summary>
    /// Publica directamente sin preview (para posts sin IA)
    /// </summary>
    public async Task<ImmediatePublishResult> PublishDirectlyAsync(Guid basePostId)
    {
        var post = await _context.BasePosts
            .Include(p => p.TargetNetworks)
            .FirstOrDefaultAsync(p => p.Id == basePostId)
            ?? throw new InvalidOperationException($"Post no encontrado: {basePostId}");

        // Crear diccionario con contenido original para cada red
        var contentByNetwork = post.TargetNetworks.ToDictionary(
            tn => tn.NetworkType,
            tn => post.Content
        );

        return await PublishAfterPreviewAsync(basePostId, contentByNetwork);
    }
}

#region DTOs

/// <summary>
/// Preview del contenido adaptado para una red
/// </summary>
public class AdaptedContentPreview
{
    public NetworkType NetworkType { get; set; }
    public string OriginalContent { get; set; } = string.Empty;
    public string AdaptedContent { get; set; } = string.Empty;
    public int CharacterCount { get; set; }
    public bool UsedAi { get; set; }
    public bool IncludeMedia { get; set; }
}

/// <summary>
/// Resultado de publicación inmediata
/// </summary>
public class ImmediatePublishResult
{
    public Guid BasePostId { get; set; }
    public bool OverallSuccess { get; set; }
    public string? OverallMessage { get; set; }
    public Dictionary<NetworkType, NetworkPublishResult> NetworkResults { get; set; } = new();
}

/// <summary>
/// Resultado de publicación en una red específica
/// </summary>
public class NetworkPublishResult
{
    public bool Success { get; set; }
    public string? ExternalId { get; set; }
    public string? ErrorMessage { get; set; }
}

#endregion
```

---

### Tarea 4.4: Crear Interfaz IImmediatePublishService

**Archivo a crear:** `SocialPanelCore.Domain/Interfaces/IImmediatePublishService.cs`

```csharp
using SocialPanelCore.Domain.Enums;

namespace SocialPanelCore.Domain.Interfaces;

/// <summary>
/// Servicio para publicación inmediata (síncrona)
/// </summary>
public interface IImmediatePublishService
{
    /// <summary>
    /// Genera previews del contenido adaptado sin guardar en BD
    /// </summary>
    Task<Dictionary<NetworkType, AdaptedContentPreview>> GeneratePreviewsAsync(Guid basePostId);

    /// <summary>
    /// Publica después de que el usuario confirme/edite los previews
    /// </summary>
    Task<ImmediatePublishResult> PublishAfterPreviewAsync(Guid basePostId, Dictionary<NetworkType, string> editedContent);

    /// <summary>
    /// Publica directamente sin preview (para posts sin IA)
    /// </summary>
    Task<ImmediatePublishResult> PublishDirectlyAsync(Guid basePostId);
}

// Las clases DTO (AdaptedContentPreview, ImmediatePublishResult, NetworkPublishResult)
// ya están definidas en ImmediatePublishService.cs
```

**Nota:** Mover las clases DTO a un archivo separado si se prefiere:
`SocialPanelCore.Domain/DTOs/PublishDtos.cs`

---

### Tarea 4.5: Crear Página de Preview

**Archivo a crear:** `Components/Pages/Publications/Preview.razor`

```razor
@page "/publications/preview/{Id:guid}"
@rendermode InteractiveServer
@using SocialPanelCore.Domain.Interfaces
@using SocialPanelCore.Domain.Entities
@using SocialPanelCore.Domain.Enums
@inject IBasePostService BasePostService
@inject IImmediatePublishService ImmediatePublishService
@inject ISnackbar Snackbar
@inject NavigationManager Navigation

<PageTitle>Preview de Publicación</PageTitle>

@if (_loading)
{
    <MudProgressLinear Color="Color.Primary" Indeterminate="true" />
    <MudText Typo="Typo.body1" Class="mt-4">Generando adaptaciones con IA...</MudText>
    <MudText Typo="Typo.caption" Color="Color.Secondary">Esto puede tardar unos segundos</MudText>
}
else if (_post == null)
{
    <MudAlert Severity="Severity.Error">Publicación no encontrada</MudAlert>
}
else
{
    <MudText Typo="Typo.h4" Class="mb-2">Preview de Publicación</MudText>
    <MudText Typo="Typo.subtitle1" Color="Color.Secondary" Class="mb-4">
        Revisa y edita el contenido adaptado antes de publicar
    </MudText>

    <MudGrid>
        <!-- Panel de previews -->
        <MudItem xs="12" md="8">
            <MudText Typo="Typo.h6" Class="mb-2">Contenido por Red Social</MudText>

            @foreach (var preview in _previews.Values)
            {
                <MudCard Elevation="2" Class="mb-4">
                    <MudCardHeader>
                        <CardHeaderContent>
                            <div class="d-flex align-center">
                                <MudChip T="string" Color="Color.Primary" Size="Size.Small" Class="mr-2">
                                    @GetNetworkName(preview.NetworkType)
                                </MudChip>
                                @if (preview.UsedAi)
                                {
                                    <MudChip T="string" Color="Color.Secondary" Size="Size.Small" Variant="Variant.Outlined">
                                        Optimizado con IA
                                    </MudChip>
                                }
                                else
                                {
                                    <MudChip T="string" Color="Color.Default" Size="Size.Small" Variant="Variant.Outlined">
                                        Contenido original
                                    </MudChip>
                                }
                                @if (preview.IncludeMedia && _hasMedia)
                                {
                                    <MudChip T="string" Color="Color.Info" Size="Size.Small" Variant="Variant.Outlined" Class="ml-2">
                                        Con medios
                                    </MudChip>
                                }
                            </div>
                        </CardHeaderContent>
                        <CardHeaderActions>
                            <MudText Typo="Typo.caption">
                                @_editedContent[preview.NetworkType].Length / @GetMaxChars(preview.NetworkType) caracteres
                            </MudText>
                        </CardHeaderActions>
                    </MudCardHeader>
                    <MudCardContent>
                        <MudTextField @bind-Value="_editedContent[preview.NetworkType]"
                                      Label="Contenido"
                                      Lines="4"
                                      Variant="Variant.Outlined"
                                      HelperText="@GetHelperText(preview.NetworkType)"
                                      Counter="@GetMaxChars(preview.NetworkType)"
                                      Immediate="true" />

                        @if (preview.UsedAi && _editedContent[preview.NetworkType] != preview.AdaptedContent)
                        {
                            <MudButton Variant="Variant.Text"
                                       Color="Color.Secondary"
                                       Size="Size.Small"
                                       OnClick="@(() => ResetToOriginal(preview.NetworkType))"
                                       Class="mt-2">
                                Restaurar versión IA
                            </MudButton>
                        }
                    </MudCardContent>
                </MudCard>
            }
        </MudItem>

        <!-- Panel lateral -->
        <MudItem xs="12" md="4">
            <!-- Contenido original -->
            <MudCard Elevation="2" Class="mb-4">
                <MudCardHeader>
                    <CardHeaderContent>
                        <MudText Typo="Typo.h6">Contenido Original</MudText>
                    </CardHeaderContent>
                </MudCardHeader>
                <MudCardContent>
                    <MudText Typo="Typo.body2" Style="white-space: pre-wrap; max-height: 200px; overflow-y: auto;">
                        @_post.Content
                    </MudText>
                </MudCardContent>
            </MudCard>

            <!-- Acciones -->
            <MudCard Elevation="2">
                <MudCardContent>
                    <MudStack Spacing="2">
                        <MudButton Variant="Variant.Filled"
                                   Color="Color.Success"
                                   FullWidth="true"
                                   StartIcon="@Icons.Material.Filled.Send"
                                   OnClick="PublishNow"
                                   Disabled="@_publishing">
                            @if (_publishing)
                            {
                                <MudProgressCircular Class="ms-n1" Size="Size.Small" Indeterminate="true" />
                                <MudText Class="ms-2">Publicando...</MudText>
                            }
                            else
                            {
                                <span>Publicar Ahora</span>
                            }
                        </MudButton>

                        <MudButton Variant="Variant.Outlined"
                                   Color="Color.Warning"
                                   FullWidth="true"
                                   StartIcon="@Icons.Material.Filled.Schedule"
                                   OnClick="SaveAsScheduled"
                                   Disabled="@_publishing">
                            Guardar como Programada
                        </MudButton>

                        <MudButton Variant="Variant.Text"
                                   Color="Color.Default"
                                   FullWidth="true"
                                   StartIcon="@Icons.Material.Filled.Cancel"
                                   OnClick="Cancel"
                                   Disabled="@_publishing">
                            Cancelar
                        </MudButton>
                    </MudStack>
                </MudCardContent>
            </MudCard>
        </MudItem>
    </MudGrid>
}

@code {
    [Parameter]
    public Guid Id { get; set; }

    private BasePost? _post;
    private Dictionary<NetworkType, AdaptedContentPreview> _previews = new();
    private Dictionary<NetworkType, string> _editedContent = new();
    private bool _loading = true;
    private bool _publishing;
    private bool _hasMedia;

    protected override async Task OnInitializedAsync()
    {
        await LoadPreview();
    }

    private async Task LoadPreview()
    {
        _loading = true;
        try
        {
            _post = await BasePostService.GetPostByIdAsync(Id);
            if (_post == null)
            {
                Snackbar.Add("Publicación no encontrada", Severity.Error);
                return;
            }

            _hasMedia = _post.Media?.Any() == true;

            // Generar previews
            _previews = await ImmediatePublishService.GeneratePreviewsAsync(Id);

            // Inicializar contenido editable
            foreach (var preview in _previews)
            {
                _editedContent[preview.Key] = preview.Value.AdaptedContent;
            }
        }
        catch (Exception ex)
        {
            Snackbar.Add($"Error generando preview: {ex.Message}", Severity.Error);
        }
        finally
        {
            _loading = false;
        }
    }

    private void ResetToOriginal(NetworkType network)
    {
        if (_previews.TryGetValue(network, out var preview))
        {
            _editedContent[network] = preview.AdaptedContent;
        }
    }

    private async Task PublishNow()
    {
        _publishing = true;
        try
        {
            var result = await ImmediatePublishService.PublishAfterPreviewAsync(Id, _editedContent);

            if (result.OverallSuccess)
            {
                Snackbar.Add("Publicación completada exitosamente", Severity.Success);
                Navigation.NavigateTo($"/publications/view/{Id}");
            }
            else
            {
                var successCount = result.NetworkResults.Count(r => r.Value.Success);
                var totalCount = result.NetworkResults.Count;

                if (successCount > 0)
                {
                    Snackbar.Add($"Publicado en {successCount} de {totalCount} redes", Severity.Warning);
                }
                else
                {
                    Snackbar.Add("Error: No se pudo publicar en ninguna red", Severity.Error);
                }

                // Mostrar errores específicos
                foreach (var (network, networkResult) in result.NetworkResults.Where(r => !r.Value.Success))
                {
                    Snackbar.Add($"{network}: {networkResult.ErrorMessage}", Severity.Error);
                }

                Navigation.NavigateTo($"/publications/view/{Id}");
            }
        }
        catch (Exception ex)
        {
            Snackbar.Add($"Error: {ex.Message}", Severity.Error);
        }
        finally
        {
            _publishing = false;
        }
    }

    private async Task SaveAsScheduled()
    {
        // Guardar los AdaptedPosts pero no publicar
        // TODO: Implementar método para guardar como programada
        Snackbar.Add("Funcionalidad pendiente de implementar", Severity.Info);
    }

    private void Cancel()
    {
        Navigation.NavigateTo("/publications");
    }

    private string GetNetworkName(NetworkType network) => network switch
    {
        NetworkType.Facebook => "Facebook",
        NetworkType.Instagram => "Instagram",
        NetworkType.TikTok => "TikTok",
        NetworkType.X => "X (Twitter)",
        NetworkType.YouTube => "YouTube",
        NetworkType.LinkedIn => "LinkedIn",
        _ => network.ToString()
    };

    private int GetMaxChars(NetworkType network) => network switch
    {
        NetworkType.X => 280,
        NetworkType.TikTok => 150,
        NetworkType.Instagram => 2200,
        NetworkType.LinkedIn => 3000,
        NetworkType.Facebook => 63206,
        NetworkType.YouTube => 5000,
        _ => 5000
    };

    private string GetHelperText(NetworkType network) => network switch
    {
        NetworkType.X => "Máximo 280 caracteres",
        NetworkType.TikTok => "Máximo 150 caracteres",
        NetworkType.Instagram => "Ideal 125-150 caracteres para feed",
        NetworkType.LinkedIn => "Ideal menos de 700 caracteres",
        _ => ""
    };
}
```

---

### Tarea 4.6: Modificar New.razor para Flujo Inmediato con Preview

**Archivo a modificar:** `Components/Pages/Publications/New.razor`

Modificar el método `SaveAndSchedule` para redirigir al preview cuando es publicación inmediata con IA:

```csharp
private async Task SaveAndSchedule()
{
    await _form.Validate();
    if (!_formIsValid) return;

    // ... validaciones existentes ...

    _processing = true;
    try
    {
        var selectedNetworks = _selectedNetworks.Where(x => x.Value).Select(x => x.Key).ToList();
        var userId = await GetCurrentUserIdAsync();

        // Determinar si hay AI optimization activa
        var hasAiOptimization = _model.AiOptimizationEnabled ||
            _networkAiConfigs.Values.Any(v => v);

        DateTime scheduledDate;
        BasePostState initialState;
        PublishMode publishMode;

        if (_model.ScheduleNow)
        {
            scheduledDate = DateTime.UtcNow;
            publishMode = PublishMode.Immediate;

            if (hasAiOptimization)
            {
                // Con IA: ir al preview
                initialState = BasePostState.AdaptacionPendiente;
            }
            else
            {
                // Sin IA: publicar directamente
                initialState = BasePostState.Planificada;
            }
        }
        else
        {
            scheduledDate = _model.ScheduledDate!.Value.Date + _model.ScheduledTime!.Value;
            publishMode = PublishMode.Scheduled;
            initialState = BasePostState.Planificada;
        }

        // Crear publicación
        var post = await BasePostService.CreatePostAsync(
            _model.AccountId,
            userId,
            _model.Content,
            scheduledDate,
            selectedNetworks,
            _model.Title,
            initialState
        );

        // Guardar medios si hay
        if (_uploadedFiles.Any())
        {
            // ... código existente de guardado de medios ...
        }

        // Decidir redirección según el flujo
        if (_model.ScheduleNow && hasAiOptimization)
        {
            // Ir al preview para publicación inmediata con IA
            Navigation.NavigateTo($"/publications/preview/{post.Id}");
        }
        else if (_model.ScheduleNow && !hasAiOptimization)
        {
            // Publicar directamente sin IA
            var result = await ImmediatePublishService.PublishDirectlyAsync(post.Id);
            if (result.OverallSuccess)
            {
                Snackbar.Add("Publicación completada", Severity.Success);
            }
            else
            {
                Snackbar.Add(result.OverallMessage ?? "Publicación parcial", Severity.Warning);
            }
            Navigation.NavigateTo($"/publications/view/{post.Id}");
        }
        else
        {
            // Programada: ir al listado
            Snackbar.Add("Publicación programada exitosamente", Severity.Success);
            Navigation.NavigateTo("/publications");
        }
    }
    catch (Exception ex)
    {
        Snackbar.Add($"Error: {ex.Message}", Severity.Error);
    }
    finally
    {
        _processing = false;
    }
}
```

---

### Tarea 4.7: Registrar Nuevos Servicios en Program.cs

**Archivo a modificar:** `Program.cs`

```csharp
// Reemplazar ContentAdaptationService con AiContentService
builder.Services.AddScoped<IAiContentService, AiContentService>();

// Añadir servicio de publicación inmediata
builder.Services.AddScoped<IImmediatePublishService, ImmediatePublishService>();

// Mantener ContentAdaptationService para Hangfire (o refactorizar para usar AiContentService)
builder.Services.AddScoped<IContentAdaptationService, ContentAdaptationService>();
```

---

### Tarea 4.8: Actualizar ContentAdaptationService para Usar AiContentService

**Archivo a modificar:** `SocialPanelCore.Infrastructure/Services/ContentAdaptationService.cs`

Modificar para que use el nuevo `AiContentService`:

```csharp
public class ContentAdaptationService : IContentAdaptationService
{
    private readonly ApplicationDbContext _context;
    private readonly IAiContentService _aiContentService;
    private readonly ILogger<ContentAdaptationService> _logger;

    public ContentAdaptationService(
        ApplicationDbContext context,
        IAiContentService aiContentService,
        ILogger<ContentAdaptationService> logger)
    {
        _context = context;
        _aiContentService = aiContentService;
        _logger = logger;
    }

    public async Task AdaptPendingPostsAsync()
    {
        _logger.LogInformation("Iniciando adaptación de posts pendientes");

        var pendingPosts = await _context.BasePosts
            .Include(p => p.TargetNetworks)
            .Include(p => p.AdaptedVersions)
            .Where(p => p.State == BasePostState.AdaptacionPendiente)
            .Take(10)
            .ToListAsync();

        foreach (var post in pendingPosts)
        {
            await AdaptPostAsync(post);
        }
    }

    private async Task AdaptPostAsync(BasePost post)
    {
        var networksToAdapt = post.TargetNetworks
            .Where(tn => !post.AdaptedVersions.Any(av => av.NetworkType == tn.NetworkType))
            .ToList();

        foreach (var network in networksToAdapt)
        {
            try
            {
                // Usar el nuevo servicio de IA
                await _aiContentService.CreateAdaptedPostAsync(
                    post.Id,
                    network.NetworkType,
                    network.UseAiOptimization  // Respetar configuración por red
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adaptando post {PostId} para {Network}",
                    post.Id, network.NetworkType);
            }
        }

        // Verificar si todas las redes están adaptadas
        await _context.Entry(post).ReloadAsync();
        await _context.Entry(post).Collection(p => p.AdaptedVersions).LoadAsync();
        await _context.Entry(post).Collection(p => p.TargetNetworks).LoadAsync();

        if (post.TargetNetworks.All(tn =>
            post.AdaptedVersions.Any(av => av.NetworkType == tn.NetworkType)))
        {
            post.State = BasePostState.Adaptada;
            await _context.SaveChangesAsync();
            _logger.LogInformation("Post {PostId} completamente adaptado", post.Id);
        }
    }
}
```

---

## Criterios de Aceptación

- [ ] El servicio de IA genera contenido adaptado correctamente para cada red
- [ ] El flujo inmediato con IA muestra preview antes de publicar
- [ ] El usuario puede editar el contenido adaptado en el preview
- [ ] El flujo inmediato sin IA publica directamente
- [ ] El flujo programado funciona con Hangfire (con y sin IA)
- [ ] Los errores de IA tienen fallback al contenido original
- [ ] Los estados del post se actualizan correctamente

---

## Pruebas Manuales

1. **Publicación inmediata con IA:**
   - Crear publicación con "Publicar ahora" y AI activada
   - Verificar que se muestra la página de preview
   - Editar contenido de una red
   - Publicar y verificar resultado

2. **Publicación inmediata sin IA:**
   - Crear publicación con "Publicar ahora" y AI desactivada
   - Verificar que publica directamente sin preview

3. **Publicación programada:**
   - Crear publicación programada para el futuro
   - Verificar que Hangfire la procesa cuando llega la hora

4. **Fallback de IA:**
   - Configurar API key inválida
   - Crear publicación con IA
   - Verificar que usa fallback (contenido original truncado)

---

## Siguiente Sprint

Una vez completado este sprint, continúa con:
- **Sprint 5:** `docs/sprint5-apis-refit.md` - Integración con APIs externas usando Refit
