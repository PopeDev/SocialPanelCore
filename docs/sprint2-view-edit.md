# Sprint 2: Páginas View y Edit

**Duración estimada:** 3-4 días
**Prerrequisitos:** Sprint 1 completado

---

## Objetivo del Sprint

Implementar las páginas que actualmente dan error 404:
- `/publications/view/{id}` - Ver detalles de una publicación
- `/publications/edit/{id}` - Editar una publicación existente

---

## Tareas

### Tarea 2.1: Crear Página View.razor

**Archivo a crear:** `Components/Pages/Publications/View.razor`

Esta página muestra todos los detalles de una publicación, incluyendo:
- Información básica (título, contenido, cuenta, estado)
- Redes sociales objetivo con su configuración de AI
- Medios adjuntos (imágenes)
- Versiones adaptadas (si existen)
- Historial de estados

```razor
@page "/publications/view/{Id:guid}"
@rendermode InteractiveServer
@using Microsoft.AspNetCore.Components.Web
@using SocialPanelCore.Domain.Interfaces
@using SocialPanelCore.Domain.Entities
@using SocialPanelCore.Domain.Enums
@inject IBasePostService BasePostService
@inject IAccountService AccountService
@inject ISnackbar Snackbar
@inject NavigationManager Navigation

<PageTitle>Ver Publicación</PageTitle>

@if (_loading)
{
    <MudProgressLinear Color="Color.Primary" Indeterminate="true" />
}
else if (_post == null)
{
    <MudAlert Severity="Severity.Error">
        Publicación no encontrada
    </MudAlert>
    <MudButton Variant="Variant.Text" Color="Color.Primary" Href="/publications" Class="mt-4">
        Volver al listado
    </MudButton>
}
else
{
    <MudGrid>
        <!-- Cabecera -->
        <MudItem xs="12">
            <div class="d-flex justify-space-between align-center mb-4">
                <div>
                    <MudText Typo="Typo.h4">@_post.Title</MudText>
                    <MudText Typo="Typo.body2" Color="Color.Secondary">
                        Creada el @_post.CreatedAt.ToString("dd/MM/yyyy HH:mm")
                    </MudText>
                </div>
                <div>
                    <MudChip T="string" Color="@GetStateColor(_post.State)" Size="Size.Large">
                        @GetStateName(_post.State)
                    </MudChip>
                </div>
            </div>
        </MudItem>

        <!-- Información Principal -->
        <MudItem xs="12" md="8">
            <MudCard Elevation="2" Class="mb-4">
                <MudCardHeader>
                    <CardHeaderContent>
                        <MudText Typo="Typo.h6">Contenido Original</MudText>
                    </CardHeaderContent>
                </MudCardHeader>
                <MudCardContent>
                    <MudText Typo="Typo.body1" Style="white-space: pre-wrap;">@_post.Content</MudText>
                </MudCardContent>
            </MudCard>

            <!-- Redes Sociales Objetivo -->
            <MudCard Elevation="2" Class="mb-4">
                <MudCardHeader>
                    <CardHeaderContent>
                        <MudText Typo="Typo.h6">Redes Sociales Objetivo</MudText>
                    </CardHeaderContent>
                </MudCardHeader>
                <MudCardContent>
                    <MudSimpleTable Dense="true" Hover="true">
                        <thead>
                            <tr>
                                <th>Red Social</th>
                                <th>Optimización IA</th>
                                <th>Incluir Medios</th>
                                <th>Estado Adaptación</th>
                            </tr>
                        </thead>
                        <tbody>
                            @foreach (var network in _post.TargetNetworks)
                            {
                                var adapted = _post.AdaptedVersions?.FirstOrDefault(a => a.NetworkType == network.NetworkType);
                                <tr>
                                    <td>
                                        <MudChip T="string" Size="Size.Small" Color="Color.Primary">
                                            @GetNetworkName(network.NetworkType)
                                        </MudChip>
                                    </td>
                                    <td>
                                        @if (network.UseAiOptimization)
                                        {
                                            <MudIcon Icon="@Icons.Material.Filled.Check" Color="Color.Success" Size="Size.Small" />
                                            <span>Sí</span>
                                        }
                                        else
                                        {
                                            <MudIcon Icon="@Icons.Material.Filled.Close" Color="Color.Default" Size="Size.Small" />
                                            <span>No (contenido original)</span>
                                        }
                                    </td>
                                    <td>
                                        @if (network.IncludeMedia)
                                        {
                                            <MudIcon Icon="@Icons.Material.Filled.Image" Color="Color.Info" Size="Size.Small" />
                                            <span>Sí</span>
                                        }
                                        else
                                        {
                                            <MudIcon Icon="@Icons.Material.Filled.HideImage" Color="Color.Default" Size="Size.Small" />
                                            <span>No</span>
                                        }
                                    </td>
                                    <td>
                                        @if (adapted != null)
                                        {
                                            <MudChip T="string" Size="Size.Small" Color="@GetAdaptedStateColor(adapted.State)">
                                                @GetAdaptedStateName(adapted.State)
                                            </MudChip>
                                        }
                                        else if (!network.UseAiOptimization)
                                        {
                                            <MudText Typo="Typo.caption">N/A (sin IA)</MudText>
                                        }
                                        else
                                        {
                                            <MudText Typo="Typo.caption">Pendiente</MudText>
                                        }
                                    </td>
                                </tr>
                            }
                        </tbody>
                    </MudSimpleTable>
                </MudCardContent>
            </MudCard>

            <!-- Versiones Adaptadas -->
            @if (_post.AdaptedVersions?.Any() == true)
            {
                <MudCard Elevation="2" Class="mb-4">
                    <MudCardHeader>
                        <CardHeaderContent>
                            <MudText Typo="Typo.h6">Contenido Adaptado por Red</MudText>
                        </CardHeaderContent>
                    </MudCardHeader>
                    <MudCardContent>
                        <MudExpansionPanels MultiExpansion="true">
                            @foreach (var adapted in _post.AdaptedVersions.OrderBy(a => a.NetworkType))
                            {
                                <MudExpansionPanel>
                                    <TitleContent>
                                        <div class="d-flex align-center">
                                            <MudChip T="string" Size="Size.Small" Color="Color.Primary" Class="mr-2">
                                                @GetNetworkName(adapted.NetworkType)
                                            </MudChip>
                                            <MudChip T="string" Size="Size.Small" Color="@GetAdaptedStateColor(adapted.State)">
                                                @GetAdaptedStateName(adapted.State)
                                            </MudChip>
                                            <MudText Typo="Typo.caption" Class="ml-2">
                                                (@adapted.CharacterCount caracteres)
                                            </MudText>
                                        </div>
                                    </TitleContent>
                                    <ChildContent>
                                        <MudText Typo="Typo.body2" Style="white-space: pre-wrap;">
                                            @adapted.AdaptedContent
                                        </MudText>
                                        @if (adapted.PublishedAt.HasValue)
                                        {
                                            <MudDivider Class="my-2" />
                                            <MudText Typo="Typo.caption" Color="Color.Success">
                                                Publicado: @adapted.PublishedAt.Value.ToString("dd/MM/yyyy HH:mm")
                                            </MudText>
                                            @if (!string.IsNullOrEmpty(adapted.ExternalPostId))
                                            {
                                                <MudText Typo="Typo.caption" Color="Color.Secondary">
                                                    ID externo: @adapted.ExternalPostId
                                                </MudText>
                                            }
                                        }
                                        @if (!string.IsNullOrEmpty(adapted.LastError))
                                        {
                                            <MudDivider Class="my-2" />
                                            <MudAlert Severity="Severity.Error" Dense="true">
                                                Error: @adapted.LastError
                                            </MudAlert>
                                        }
                                    </ChildContent>
                                </MudExpansionPanel>
                            }
                        </MudExpansionPanels>
                    </MudCardContent>
                </MudCard>
            }
        </MudItem>

        <!-- Panel Lateral -->
        <MudItem xs="12" md="4">
            <!-- Información de Cuenta -->
            <MudCard Elevation="2" Class="mb-4">
                <MudCardHeader>
                    <CardHeaderContent>
                        <MudText Typo="Typo.h6">Información</MudText>
                    </CardHeaderContent>
                </MudCardHeader>
                <MudCardContent>
                    <MudList T="string" Dense="true">
                        <MudListItem T="string" Icon="@Icons.Material.Filled.Business">
                            <div class="d-flex flex-column">
                                <MudText Typo="Typo.caption">Cuenta</MudText>
                                <MudText Typo="Typo.body2">@_post.Account?.Name</MudText>
                            </div>
                        </MudListItem>
                        <MudListItem T="string" Icon="@Icons.Material.Filled.Schedule">
                            <div class="d-flex flex-column">
                                <MudText Typo="Typo.caption">Programada para</MudText>
                                <MudText Typo="Typo.body2">@_post.ScheduledAtUtc.ToString("dd/MM/yyyy HH:mm") UTC</MudText>
                            </div>
                        </MudListItem>
                        <MudListItem T="string" Icon="@Icons.Material.Filled.AutoAwesome">
                            <div class="d-flex flex-column">
                                <MudText Typo="Typo.caption">Optimización IA Global</MudText>
                                <MudText Typo="Typo.body2">
                                    @(_post.AiOptimizationEnabled ? "Activada" : "Desactivada")
                                </MudText>
                            </div>
                        </MudListItem>
                        <MudListItem T="string" Icon="@Icons.Material.Filled.PlayArrow">
                            <div class="d-flex flex-column">
                                <MudText Typo="Typo.caption">Modo de Publicación</MudText>
                                <MudText Typo="Typo.body2">
                                    @(_post.PublishMode == PublishMode.Immediate ? "Inmediata" : "Programada")
                                </MudText>
                            </div>
                        </MudListItem>
                        @if (_post.PublishedAt.HasValue)
                        {
                            <MudListItem T="string" Icon="@Icons.Material.Filled.CheckCircle">
                                <div class="d-flex flex-column">
                                    <MudText Typo="Typo.caption">Publicada</MudText>
                                    <MudText Typo="Typo.body2">@_post.PublishedAt.Value.ToString("dd/MM/yyyy HH:mm")</MudText>
                                </div>
                            </MudListItem>
                        }
                    </MudList>
                </MudCardContent>
            </MudCard>

            <!-- Medios -->
            <MudCard Elevation="2" Class="mb-4">
                <MudCardHeader>
                    <CardHeaderContent>
                        <MudText Typo="Typo.h6">Medios Adjuntos</MudText>
                    </CardHeaderContent>
                </MudCardHeader>
                <MudCardContent>
                    @if (_post.Media?.Any() == true)
                    {
                        <MudGrid>
                            @foreach (var media in _post.Media.OrderBy(m => m.SortOrder))
                            {
                                <MudItem xs="6">
                                    <MudCard>
                                        <MudCardMedia Image="@GetMediaUrl(media)" Height="100" />
                                        <MudCardContent Class="pa-2">
                                            <MudText Typo="Typo.caption">@media.OriginalFileName</MudText>
                                        </MudCardContent>
                                    </MudCard>
                                </MudItem>
                            }
                        </MudGrid>
                    }
                    else
                    {
                        <MudText Typo="Typo.body2" Color="Color.Secondary">
                            No hay medios adjuntos
                        </MudText>
                    }
                </MudCardContent>
            </MudCard>

            <!-- Acciones -->
            <MudCard Elevation="2">
                <MudCardContent>
                    <MudStack Spacing="2">
                        @if (_post.State != BasePostState.Publicada)
                        {
                            <MudButton Variant="Variant.Filled"
                                       Color="Color.Primary"
                                       FullWidth="true"
                                       StartIcon="@Icons.Material.Filled.Edit"
                                       Href="@($"/publications/edit/{_post.Id}")">
                                Editar
                            </MudButton>
                        }
                        <MudButton Variant="Variant.Outlined"
                                   Color="Color.Default"
                                   FullWidth="true"
                                   StartIcon="@Icons.Material.Filled.ArrowBack"
                                   Href="/publications">
                            Volver al Listado
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
    private bool _loading = true;

    protected override async Task OnInitializedAsync()
    {
        await LoadPost();
    }

    private async Task LoadPost()
    {
        _loading = true;
        try
        {
            _post = await BasePostService.GetPostByIdAsync(Id);
            if (_post == null)
            {
                Snackbar.Add("Publicación no encontrada", Severity.Error);
            }
        }
        catch (Exception ex)
        {
            Snackbar.Add($"Error al cargar la publicación: {ex.Message}", Severity.Error);
        }
        finally
        {
            _loading = false;
        }
    }

    private string GetMediaUrl(PostMedia media)
    {
        // TODO: Implementar cuando exista MediaStorageService
        return $"/uploads/{media.RelativePath}";
    }

    private Color GetStateColor(BasePostState state)
    {
        return state switch
        {
            BasePostState.Borrador => Color.Default,
            BasePostState.Planificada => Color.Info,
            BasePostState.AdaptacionPendiente => Color.Warning,
            BasePostState.Adaptada => Color.Primary,
            BasePostState.ParcialmentePublicada => Color.Warning,
            BasePostState.Publicada => Color.Success,
            BasePostState.Cancelada => Color.Error,
            _ => Color.Default
        };
    }

    private string GetStateName(BasePostState state)
    {
        return state switch
        {
            BasePostState.Borrador => "Borrador",
            BasePostState.Planificada => "Planificada",
            BasePostState.AdaptacionPendiente => "Pendiente Adaptación",
            BasePostState.Adaptada => "Adaptada",
            BasePostState.ParcialmentePublicada => "Parcialmente Publicada",
            BasePostState.Publicada => "Publicada",
            BasePostState.Cancelada => "Cancelada",
            _ => state.ToString()
        };
    }

    private Color GetAdaptedStateColor(AdaptedPostState state)
    {
        return state switch
        {
            AdaptedPostState.Pending => Color.Warning,
            AdaptedPostState.Ready => Color.Info,
            AdaptedPostState.Published => Color.Success,
            AdaptedPostState.Failed => Color.Error,
            _ => Color.Default
        };
    }

    private string GetAdaptedStateName(AdaptedPostState state)
    {
        return state switch
        {
            AdaptedPostState.Pending => "Pendiente",
            AdaptedPostState.Ready => "Lista",
            AdaptedPostState.Published => "Publicada",
            AdaptedPostState.Failed => "Fallida",
            _ => state.ToString()
        };
    }

    private string GetNetworkName(NetworkType network)
    {
        return network switch
        {
            NetworkType.Facebook => "Facebook",
            NetworkType.Instagram => "Instagram",
            NetworkType.TikTok => "TikTok",
            NetworkType.X => "X (Twitter)",
            NetworkType.YouTube => "YouTube",
            NetworkType.LinkedIn => "LinkedIn",
            _ => network.ToString()
        };
    }
}
```

---

### Tarea 2.2: Crear Página Edit.razor

**Archivo a crear:** `Components/Pages/Publications/Edit.razor`

Esta página permite editar una publicación existente. Solo se puede editar si el estado NO es `Publicada`.

```razor
@page "/publications/edit/{Id:guid}"
@rendermode InteractiveServer
@using Microsoft.AspNetCore.Components.Web
@using SocialPanelCore.Domain.Interfaces
@using SocialPanelCore.Domain.Entities
@using SocialPanelCore.Domain.Enums
@inject IBasePostService BasePostService
@inject IAccountService AccountService
@inject ISnackbar Snackbar
@inject NavigationManager Navigation

<PageTitle>Editar Publicación</PageTitle>

@if (_loading)
{
    <MudProgressLinear Color="Color.Primary" Indeterminate="true" />
}
else if (_post == null)
{
    <MudAlert Severity="Severity.Error">
        Publicación no encontrada
    </MudAlert>
    <MudButton Variant="Variant.Text" Color="Color.Primary" Href="/publications" Class="mt-4">
        Volver al listado
    </MudButton>
}
else if (_post.State == BasePostState.Publicada)
{
    <MudAlert Severity="Severity.Warning">
        No se puede editar una publicación ya publicada
    </MudAlert>
    <MudButton Variant="Variant.Text" Color="Color.Primary" Href="@($"/publications/view/{Id}")" Class="mt-4">
        Ver publicación
    </MudButton>
}
else
{
    <MudText Typo="Typo.h4" Class="mb-4">Editar Publicación</MudText>

    <MudForm @ref="_form" @bind-IsValid="@_formIsValid">
        <MudGrid>
            <!-- Información Básica -->
            <MudItem xs="12" md="8">
                <MudCard Elevation="2" Class="mb-4">
                    <MudCardHeader>
                        <CardHeaderContent>
                            <MudText Typo="Typo.h6">Información Básica</MudText>
                        </CardHeaderContent>
                        <CardHeaderActions>
                            <MudChip T="string" Color="@GetStateColor(_post.State)" Size="Size.Small">
                                @GetStateName(_post.State)
                            </MudChip>
                        </CardHeaderActions>
                    </MudCardHeader>
                    <MudCardContent>
                        <MudTextField Label="Cuenta"
                                      Value="@_post.Account?.Name"
                                      ReadOnly="true"
                                      Disabled="true"
                                      Variant="Variant.Outlined"
                                      AdornmentIcon="@Icons.Material.Filled.Business"
                                      Adornment="Adornment.Start" />

                        <MudTextField Label="Título"
                                      @bind-Value="_model.Title"
                                      Required="true"
                                      RequiredError="El título es obligatorio"
                                      MaxLength="200"
                                      Counter="200"
                                      Immediate="true"
                                      Class="mt-4" />

                        <MudTextField Label="Contenido Original"
                                      @bind-Value="_model.Content"
                                      Required="true"
                                      RequiredError="El contenido es obligatorio"
                                      Lines="6"
                                      MaxLength="5000"
                                      Counter="5000"
                                      Immediate="true"
                                      Class="mt-4" />

                        <MudAlert Severity="Severity.Info" Class="mt-4" Dense="true">
                            @if (_post.State == BasePostState.Adaptada)
                            {
                                <span>Al modificar el contenido, las versiones adaptadas se regenerarán automáticamente.</span>
                            }
                            else
                            {
                                <span>El contenido será adaptado para cada red social según la configuración de IA.</span>
                            }
                        </MudAlert>
                    </MudCardContent>
                </MudCard>

                <!-- Programación -->
                <MudCard Elevation="2" Class="mb-4">
                    <MudCardHeader>
                        <CardHeaderContent>
                            <MudText Typo="Typo.h6">Programación</MudText>
                        </CardHeaderContent>
                    </MudCardHeader>
                    <MudCardContent>
                        <MudDatePicker Label="Fecha de Publicación"
                                       @bind-Date="_model.ScheduledDate"
                                       MinDate="DateTime.Today"
                                       Required="true" />

                        <MudTimePicker Label="Hora de Publicación"
                                       @bind-Time="_model.ScheduledTime"
                                       Required="true"
                                       Class="mt-4" />
                    </MudCardContent>
                </MudCard>

                <!-- Redes Sociales -->
                <MudCard Elevation="2">
                    <MudCardHeader>
                        <CardHeaderContent>
                            <MudText Typo="Typo.h6">Configuración por Red Social</MudText>
                        </CardHeaderContent>
                    </MudCardHeader>
                    <MudCardContent>
                        <!-- Checkbox Global de AI -->
                        <MudCheckBox @bind-Value="_model.AiOptimizationEnabled"
                                     Label="Optimizar contenido con IA (global)"
                                     Color="Color.Primary"
                                     ValueChanged="@OnGlobalAiChanged" />

                        <MudDivider Class="my-4" />

                        <MudText Typo="Typo.subtitle2" Class="mb-2">Configuración individual por red:</MudText>

                        <MudSimpleTable Dense="true" Hover="true">
                            <thead>
                                <tr>
                                    <th>Red Social</th>
                                    <th>Usar IA</th>
                                    <th>Incluir Medios</th>
                                </tr>
                            </thead>
                            <tbody>
                                @foreach (var networkConfig in _networkConfigs)
                                {
                                    <tr>
                                        <td>
                                            <MudChip T="string" Size="Size.Small" Color="Color.Primary">
                                                @GetNetworkName(networkConfig.NetworkType)
                                            </MudChip>
                                        </td>
                                        <td>
                                            <MudCheckBox T="bool"
                                                         Value="@networkConfig.UseAiOptimization"
                                                         ValueChanged="@((bool val) => OnNetworkAiChanged(networkConfig, val))"
                                                         Color="Color.Secondary"
                                                         Dense="true" />
                                        </td>
                                        <td>
                                            <MudCheckBox T="bool"
                                                         @bind-Value="@networkConfig.IncludeMedia"
                                                         Color="Color.Info"
                                                         Dense="true"
                                                         Disabled="@(!HasMedia)" />
                                        </td>
                                    </tr>
                                }
                            </tbody>
                        </MudSimpleTable>

                        @if (!HasMedia)
                        {
                            <MudText Typo="Typo.caption" Color="Color.Secondary" Class="mt-2">
                                * La publicación no tiene medios adjuntos, la opción "Incluir Medios" está deshabilitada.
                            </MudText>
                        }
                    </MudCardContent>
                </MudCard>
            </MudItem>

            <!-- Panel Lateral -->
            <MudItem xs="12" md="4">
                <!-- Medios -->
                <MudCard Elevation="2" Class="mb-4">
                    <MudCardHeader>
                        <CardHeaderContent>
                            <MudText Typo="Typo.h6">Medios Adjuntos</MudText>
                        </CardHeaderContent>
                    </MudCardHeader>
                    <MudCardContent>
                        @if (_post.Media?.Any() == true)
                        {
                            <MudGrid>
                                @foreach (var media in _post.Media.OrderBy(m => m.SortOrder))
                                {
                                    <MudItem xs="6">
                                        <MudCard>
                                            <MudCardMedia Image="@GetMediaUrl(media)" Height="80" />
                                            <MudCardContent Class="pa-1">
                                                <MudText Typo="Typo.caption" Class="text-truncate">
                                                    @media.OriginalFileName
                                                </MudText>
                                            </MudCardContent>
                                        </MudCard>
                                    </MudItem>
                                }
                            </MudGrid>
                            <MudText Typo="Typo.caption" Color="Color.Secondary" Class="mt-2">
                                Para modificar los medios, cree una nueva publicación.
                            </MudText>
                        }
                        else
                        {
                            <MudText Typo="Typo.body2" Color="Color.Secondary">
                                No hay medios adjuntos
                            </MudText>
                        }
                    </MudCardContent>
                </MudCard>

                <!-- Previsualización -->
                <MudCard Elevation="2" Class="mb-4">
                    <MudCardHeader>
                        <CardHeaderContent>
                            <MudText Typo="Typo.h6">Previsualización</MudText>
                        </CardHeaderContent>
                    </MudCardHeader>
                    <MudCardContent>
                        <MudText Typo="Typo.body2" Class="mb-2"><strong>Título:</strong></MudText>
                        <MudText Typo="Typo.body2" Color="@(_model.Title?.Length > 0 ? Color.Default : Color.Secondary)">
                            @(_model.Title?.Length > 0 ? _model.Title : "Sin título")
                        </MudText>

                        <MudDivider Class="my-3" />

                        <MudText Typo="Typo.body2" Class="mb-2"><strong>Contenido:</strong></MudText>
                        <MudText Typo="Typo.body2"
                                 Color="@(_model.Content?.Length > 0 ? Color.Default : Color.Secondary)"
                                 Style="max-height: 150px; overflow-y: auto; white-space: pre-wrap;">
                            @(_model.Content?.Length > 0 ? _model.Content : "Sin contenido")
                        </MudText>

                        <MudDivider Class="my-3" />

                        <MudText Typo="Typo.body2" Class="mb-2"><strong>Programada para:</strong></MudText>
                        <MudText Typo="Typo.body2">
                            @if (_model.ScheduledDate.HasValue && _model.ScheduledTime.HasValue)
                            {
                                @((_model.ScheduledDate.Value.Date + _model.ScheduledTime.Value).ToString("dd/MM/yyyy HH:mm"))
                            }
                            else
                            {
                                <span class="mud-text-secondary">No definida</span>
                            }
                        </MudText>
                    </MudCardContent>
                </MudCard>

                <!-- Acciones -->
                <MudCard Elevation="2">
                    <MudCardContent>
                        <MudStack Spacing="2">
                            <MudButton Variant="Variant.Filled"
                                       Color="Color.Primary"
                                       FullWidth="true"
                                       StartIcon="@Icons.Material.Filled.Save"
                                       OnClick="SaveChanges"
                                       Disabled="@(!_formIsValid || _saving)">
                                @if (_saving)
                                {
                                    <MudProgressCircular Class="ms-n1" Size="Size.Small" Indeterminate="true" />
                                    <MudText Class="ms-2">Guardando...</MudText>
                                }
                                else
                                {
                                    <span>Guardar Cambios</span>
                                }
                            </MudButton>
                            <MudButton Variant="Variant.Outlined"
                                       Color="Color.Default"
                                       FullWidth="true"
                                       StartIcon="@Icons.Material.Filled.Cancel"
                                       Href="@($"/publications/view/{Id}")"
                                       Disabled="@_saving">
                                Cancelar
                            </MudButton>
                        </MudStack>
                    </MudCardContent>
                </MudCard>
            </MudItem>
        </MudGrid>
    </MudForm>
}

@code {
    [Parameter]
    public Guid Id { get; set; }

    private MudForm _form = null!;
    private bool _formIsValid;
    private bool _loading = true;
    private bool _saving;

    private BasePost? _post;
    private EditModel _model = new();
    private List<NetworkConfigModel> _networkConfigs = new();

    private bool HasMedia => _post?.Media?.Any() == true;

    protected override async Task OnInitializedAsync()
    {
        await LoadPost();
    }

    private async Task LoadPost()
    {
        _loading = true;
        try
        {
            _post = await BasePostService.GetPostByIdAsync(Id);
            if (_post != null)
            {
                // Cargar modelo de edición
                _model = new EditModel
                {
                    Title = _post.Title ?? string.Empty,
                    Content = _post.Content,
                    ScheduledDate = _post.ScheduledAtUtc.Date,
                    ScheduledTime = _post.ScheduledAtUtc.TimeOfDay,
                    AiOptimizationEnabled = _post.AiOptimizationEnabled
                };

                // Cargar configuración de redes
                _networkConfigs = _post.TargetNetworks.Select(tn => new NetworkConfigModel
                {
                    Id = tn.Id,
                    NetworkType = tn.NetworkType,
                    UseAiOptimization = tn.UseAiOptimization,
                    IncludeMedia = tn.IncludeMedia
                }).ToList();
            }
        }
        catch (Exception ex)
        {
            Snackbar.Add($"Error al cargar la publicación: {ex.Message}", Severity.Error);
        }
        finally
        {
            _loading = false;
        }
    }

    private void OnGlobalAiChanged(bool value)
    {
        _model.AiOptimizationEnabled = value;
        // Actualizar todos los checkboxes individuales
        foreach (var config in _networkConfigs)
        {
            config.UseAiOptimization = value;
        }
    }

    private void OnNetworkAiChanged(NetworkConfigModel config, bool value)
    {
        config.UseAiOptimization = value;
        // Actualizar el global si todos están iguales
        _model.AiOptimizationEnabled = _networkConfigs.All(c => c.UseAiOptimization);
    }

    private async Task SaveChanges()
    {
        await _form.Validate();
        if (!_formIsValid) return;

        _saving = true;
        try
        {
            // Calcular fecha programada
            var scheduledDate = _model.ScheduledDate!.Value.Date + _model.ScheduledTime!.Value;

            // Actualizar post base
            await BasePostService.UpdatePostAsync(
                Id,
                _model.Content,
                _model.Title,
                scheduledDate
            );

            // TODO: Actualizar configuración de redes (UseAiOptimization, IncludeMedia)
            // Esto requiere extender IBasePostService o crear un método específico

            Snackbar.Add("Publicación actualizada exitosamente", Severity.Success);
            Navigation.NavigateTo($"/publications/view/{Id}");
        }
        catch (Exception ex)
        {
            Snackbar.Add($"Error al guardar: {ex.Message}", Severity.Error);
        }
        finally
        {
            _saving = false;
        }
    }

    private string GetMediaUrl(PostMedia media)
    {
        return $"/uploads/{media.RelativePath}";
    }

    private Color GetStateColor(BasePostState state)
    {
        return state switch
        {
            BasePostState.Borrador => Color.Default,
            BasePostState.Planificada => Color.Info,
            BasePostState.AdaptacionPendiente => Color.Warning,
            BasePostState.Adaptada => Color.Primary,
            BasePostState.ParcialmentePublicada => Color.Warning,
            BasePostState.Publicada => Color.Success,
            BasePostState.Cancelada => Color.Error,
            _ => Color.Default
        };
    }

    private string GetStateName(BasePostState state)
    {
        return state switch
        {
            BasePostState.Borrador => "Borrador",
            BasePostState.Planificada => "Planificada",
            BasePostState.AdaptacionPendiente => "Pendiente Adaptación",
            BasePostState.Adaptada => "Adaptada",
            BasePostState.ParcialmentePublicada => "Parcialmente Publicada",
            BasePostState.Publicada => "Publicada",
            BasePostState.Cancelada => "Cancelada",
            _ => state.ToString()
        };
    }

    private string GetNetworkName(NetworkType network)
    {
        return network switch
        {
            NetworkType.Facebook => "Facebook",
            NetworkType.Instagram => "Instagram",
            NetworkType.TikTok => "TikTok",
            NetworkType.X => "X (Twitter)",
            NetworkType.YouTube => "YouTube",
            NetworkType.LinkedIn => "LinkedIn",
            _ => network.ToString()
        };
    }

    private class EditModel
    {
        public string Title { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public DateTime? ScheduledDate { get; set; }
        public TimeSpan? ScheduledTime { get; set; }
        public bool AiOptimizationEnabled { get; set; } = true;
    }

    private class NetworkConfigModel
    {
        public Guid Id { get; set; }
        public NetworkType NetworkType { get; set; }
        public bool UseAiOptimization { get; set; }
        public bool IncludeMedia { get; set; }
    }
}
```

---

### Tarea 2.3: Extender IBasePostService

**Archivo a modificar:** `SocialPanelCore.Domain/Interfaces/IBasePostService.cs`

Añadir el método para actualizar la configuración de redes:

```csharp
// Añadir al final de la interfaz, antes de la llave de cierre

/// <summary>
/// Actualiza la configuración de AI y medios para las redes de una publicación
/// </summary>
Task UpdateNetworkConfigsAsync(Guid postId, List<NetworkConfigUpdate> configs);
```

**Añadir también la clase de DTO (puede ir en el mismo archivo o en uno separado):**

```csharp
/// <summary>
/// DTO para actualizar la configuración de una red en una publicación
/// </summary>
public class NetworkConfigUpdate
{
    public Guid NetworkId { get; set; }
    public bool UseAiOptimization { get; set; }
    public bool IncludeMedia { get; set; }
}
```

---

### Tarea 2.4: Implementar el Método en BasePostService

**Archivo a modificar:** `SocialPanelCore.Infrastructure/Services/BasePostService.cs`

Añadir el método al final de la clase:

```csharp
public async Task UpdateNetworkConfigsAsync(Guid postId, List<NetworkConfigUpdate> configs)
{
    var post = await _context.BasePosts
        .Include(p => p.TargetNetworks)
        .FirstOrDefaultAsync(p => p.Id == postId)
        ?? throw new InvalidOperationException($"Post no encontrado: {postId}");

    if (post.State == BasePostState.Publicada)
        throw new InvalidOperationException("No se puede modificar un post ya publicado");

    foreach (var config in configs)
    {
        var network = post.TargetNetworks.FirstOrDefault(tn => tn.Id == config.NetworkId);
        if (network != null)
        {
            network.UseAiOptimization = config.UseAiOptimization;
            network.IncludeMedia = config.IncludeMedia;
        }
    }

    post.UpdatedAt = DateTime.UtcNow;

    // Si se cambió la configuración de AI y el post ya estaba adaptado, volver a pendiente
    if (post.State == BasePostState.Adaptada)
    {
        post.State = BasePostState.AdaptacionPendiente;
        _logger.LogInformation("Post {PostId} vuelve a AdaptacionPendiente por cambio de configuración", postId);
    }

    await _context.SaveChangesAsync();
    _logger.LogInformation("Configuración de redes actualizada para post {PostId}", postId);
}
```

---

### Tarea 2.5: Actualizar GetPostByIdAsync para Incluir Media

**Archivo a modificar:** `SocialPanelCore.Infrastructure/Services/BasePostService.cs`

Modificar el método `GetPostByIdAsync` para incluir los medios:

```csharp
public async Task<BasePost?> GetPostByIdAsync(Guid id)
{
    return await _context.BasePosts
        .Include(p => p.TargetNetworks)
        .Include(p => p.Account)
        .Include(p => p.CreatedByUser)
        .Include(p => p.AdaptedVersions)  // AÑADIR ESTA LÍNEA
        .Include(p => p.Media)             // AÑADIR ESTA LÍNEA
        .FirstOrDefaultAsync(p => p.Id == id);
}
```

---

### Tarea 2.6: Completar Edit.razor - Llamada a UpdateNetworkConfigsAsync

**Modificar** el método `SaveChanges` en `Edit.razor`:

Reemplazar el TODO con la implementación real:

```csharp
private async Task SaveChanges()
{
    await _form.Validate();
    if (!_formIsValid) return;

    _saving = true;
    try
    {
        // Calcular fecha programada
        var scheduledDate = _model.ScheduledDate!.Value.Date + _model.ScheduledTime!.Value;

        // Actualizar post base
        await BasePostService.UpdatePostAsync(
            Id,
            _model.Content,
            _model.Title,
            scheduledDate
        );

        // Actualizar configuración de AI global
        // TODO: Añadir método para actualizar AiOptimizationEnabled en BasePost

        // Actualizar configuración de redes
        var networkUpdates = _networkConfigs.Select(nc => new NetworkConfigUpdate
        {
            NetworkId = nc.Id,
            UseAiOptimization = nc.UseAiOptimization,
            IncludeMedia = nc.IncludeMedia
        }).ToList();

        await BasePostService.UpdateNetworkConfigsAsync(Id, networkUpdates);

        Snackbar.Add("Publicación actualizada exitosamente", Severity.Success);
        Navigation.NavigateTo($"/publications/view/{Id}");
    }
    catch (Exception ex)
    {
        Snackbar.Add($"Error al guardar: {ex.Message}", Severity.Error);
    }
    finally
    {
        _saving = false;
    }
}
```

**Nota:** No olvides añadir el using para NetworkConfigUpdate en Edit.razor.

---

## Criterios de Aceptación

- [ ] La página `/publications/view/{id}` muestra todos los detalles de la publicación
- [ ] La página `/publications/view/{id}` muestra las versiones adaptadas si existen
- [ ] La página `/publications/edit/{id}` permite editar título, contenido y fecha
- [ ] La página `/publications/edit/{id}` permite configurar AI por red
- [ ] La página `/publications/edit/{id}` permite configurar medios por red
- [ ] No se puede editar una publicación ya publicada
- [ ] Al guardar cambios, si el post estaba adaptado, vuelve a estado pendiente
- [ ] Los botones de navegación funcionan correctamente

---

## Pruebas Manuales

1. **Ver publicación existente:**
   - Ir a `/publications`
   - Hacer clic en el icono de "Ver" (ojo)
   - Verificar que se muestran todos los datos

2. **Editar publicación:**
   - Desde la vista de publicación, hacer clic en "Editar"
   - Modificar el título y contenido
   - Cambiar la configuración de AI para una red
   - Guardar y verificar los cambios

3. **Intentar editar publicación publicada:**
   - Buscar una publicación con estado "Publicada"
   - Verificar que el botón de editar está deshabilitado o muestra mensaje de error

---

## Siguiente Sprint

Una vez completado este sprint, continúa con:
- **Sprint 3:** `docs/sprint3-medios.md` - Sistema de almacenamiento de medios
