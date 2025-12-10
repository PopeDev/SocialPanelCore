# 🤖 Instrucciones para GitHub Copilot - SocialPanelCore

> **Versión**: 1.0  
> **Última actualización**: 10 de diciembre de 2025  
> **Proyecto**: SocialPanelCore - Panel de Gestión de Redes Sociales con Blazor

---

## 📌 Reglas de Oro (LEER PRIMERO)

### 🚨 REGLA CRÍTICA #1: Render Modes en Blazor

**ANTES de crear o modificar cualquier componente Razor, pregúntate:**

> ¿Este componente tiene eventos interactivos (`@onclick`, `@onchange`, formularios, diálogos)?

- **SI**: Agrega `@rendermode InteractiveServer` **como PRIMERA LÍNEA** del archivo.
- **NO**: No agregues directiva (usará Static SSR por defecto).

#### ✅ Sintaxis CORRECTA (componente interactivo):

```razor
@rendermode InteractiveServer
@page "/mi-pagina"
@using MiNamespace
@inject IServicio Servicio

<MudButton OnClick="MiMetodo">Acción</MudButton>

@code {
    private void MiMetodo() { }
}
```

#### ❌ Sintaxis INCORRECTA (causará botones que no funcionan):

```razor
@page "/mi-pagina"
@using MiNamespace
@rendermode InteractiveServer  // ❌ TARDE - ya no aplica
```

```razor
@using MiNamespace
@rendermode InteractiveServer  // ❌ DESPUÉS de @using
@page "/mi-pagina"
```

#### 🔴 Componentes que SIEMPRE necesitan `@rendermode InteractiveServer`:

- ✅ Diálogos (`MudDialog`, `AccountDialog`, `ConfirmDialog`, etc.)
- ✅ Páginas con botones de acción
- ✅ Formularios con validación en tiempo real
- ✅ Componentes con `@bind-Value`, `@onclick`, `@onchange`
- ✅ Tablas con botones de editar/eliminar
- ✅ Cualquier componente que use MudBlazor con eventos

#### 🟢 Componentes que NO necesitan rendermode:

- ✅ Páginas de solo lectura (informativas)
- ✅ Componentes estáticos (sin eventos)
- ✅ Layouts (`MainLayout.razor` - NUNCA agregar rendermode aquí)

---

### 🚨 REGLA CRÍTICA #2: Orden de Directivas en Razor

**ORDEN OBLIGATORIO al inicio de archivos `.razor` interactivos:**

```razor
@rendermode InteractiveServer    // 1️⃣ SIEMPRE PRIMERO
@page "/ruta"                     // 2️⃣ Después (si es página)
@using Namespace1                 // 3️⃣ Usings
@using Namespace2
@inject IServicio Servicio        // 4️⃣ Inyecciones
@attribute [Authorize]            // 5️⃣ Atributos (si aplica)
```

**NUNCA intercales directivas en otro orden.**

---

### 🚨 REGLA CRÍTICA #3: Validación de Cambios

**DESPUÉS de crear/modificar archivos Razor con eventos:**

1. **Verificar** que `@rendermode InteractiveServer` esté en la **línea 1**.
2. **Probar** que los botones respondan (no solo que compilé).
3. **Revisar consola del navegador** para errores de SignalR/Blazor.

---

## 🏗️ Arquitectura del Proyecto

### Estructura de Capas (Clean Architecture)

```
SocialPanelCore/
├── Components/              # UI (Blazor)
│   ├── Pages/              # Páginas con @page
│   │   ├── Accounts/       # Gestión de cuentas
│   │   ├── Publications/   # Gestión de publicaciones
│   │   ├── Reviews/        # Revisión de contenido
│   │   └── SocialChannels/ # Canales de redes sociales
│   ├── Layout/             # MainLayout.razor (NO rendermode)
│   └── Shared/             # Componentes reutilizables
│       └── Dialogs/        # Diálogos (SIEMPRE InteractiveServer)
├── Data/                   # DbContext + Migrations (EF Core)
├── Domain/                 # Entidades + Interfaces
├── Application/            # Servicios + Lógica de negocio
└── Infrastructure/         # Implementaciones concretas
```

### Tecnologías Clave

- **.NET 10.0**: Framework principal
- **Blazor United**: SSR + InteractiveServer + WebAssembly
- **MudBlazor**: Componentes UI (requiere `@rendermode InteractiveServer` para eventos)
- **PostgreSQL**: Base de datos (vía EF Core)
- **FluentValidation**: Validación de modelos
- **Serilog**: Logging estructurado
- **Polly**: Resiliencia en HttpClient

---

## 🎯 Patrones de Código Comunes

### 1. Crear una Nueva Página con Tabla CRUD

```razor
@rendermode InteractiveServer
@page "/entidades"
@using MiApp.Domain.Interfaces
@using MiApp.Domain.Entities
@inject IEntidadService EntidadService
@inject IDialogService DialogService
@inject ISnackbar Snackbar

<PageTitle>Gestión de Entidades</PageTitle>

<MudText Typo="Typo.h4" Class="mb-4">Gestión de Entidades</MudText>

<MudCard Elevation="2">
    <MudCardContent>
        <div class="d-flex justify-space-between mb-4">
            <MudTextField @bind-Value="_searchString"
                          Placeholder="Buscar..."
                          Adornment="Adornment.Start"
                          AdornmentIcon="@Icons.Material.Filled.Search" />
            <MudButton Variant="Variant.Filled"
                       Color="Color.Primary"
                       StartIcon="@Icons.Material.Filled.Add"
                       OnClick="OpenCreateDialog">
                Nueva Entidad
            </MudButton>
        </div>

        <MudTable Items="@_filteredItems" Dense="true" Hover="true">
            <HeaderContent>
                <MudTh>Nombre</MudTh>
                <MudTh Style="text-align: right">Acciones</MudTh>
            </HeaderContent>
            <RowTemplate>
                <MudTd>@context.Name</MudTd>
                <MudTd Style="text-align: right">
                    <MudIconButton Icon="@Icons.Material.Filled.Edit"
                                   Color="Color.Primary"
                                   Size="Size.Small"
                                   OnClick="@(() => OpenEditDialog(context))" />
                    <MudIconButton Icon="@Icons.Material.Filled.Delete"
                                   Color="Color.Error"
                                   Size="Size.Small"
                                   OnClick="@(() => DeleteItem(context))" />
                </MudTd>
            </RowTemplate>
        </MudTable>
    </MudCardContent>
</MudCard>

@code {
    private List<Entidad> _items = new();
    private List<Entidad> _filteredItems = new();
    private string _searchString = string.Empty;

    protected override async Task OnInitializedAsync()
    {
        await LoadItems();
    }

    private async Task LoadItems()
    {
        _items = (await EntidadService.GetAllAsync()).ToList();
        FilterItems();
    }

    private void FilterItems()
    {
        _filteredItems = string.IsNullOrWhiteSpace(_searchString)
            ? _items
            : _items.Where(e => e.Name.Contains(_searchString, StringComparison.OrdinalIgnoreCase)).ToList();
    }

    private async Task OpenCreateDialog()
    {
        var parameters = new DialogParameters
        {
            { nameof(EntidadDialog.IsEditMode), false }
        };

        var options = new DialogOptions { MaxWidth = MaxWidth.Medium, FullWidth = true };
        var dialog = await DialogService.ShowAsync<EntidadDialog>("Nueva Entidad", parameters, options);
        var result = await dialog.Result;

        if (result is not null && !result.Canceled)
        {
            await LoadItems();
            Snackbar.Add("Entidad creada exitosamente", Severity.Success);
        }
    }

    private async Task OpenEditDialog(Entidad entidad)
    {
        var parameters = new DialogParameters
        {
            { nameof(EntidadDialog.Entidad), entidad },
            { nameof(EntidadDialog.IsEditMode), true }
        };

        var dialog = await DialogService.ShowAsync<EntidadDialog>("Editar Entidad", parameters);
        var result = await dialog.Result;

        if (result is not null && !result.Canceled)
        {
            await LoadItems();
            Snackbar.Add("Entidad actualizada", Severity.Success);
        }
    }

    private async Task DeleteItem(Entidad entidad)
    {
        // Implementar confirmación con ConfirmDialog
        await EntidadService.DeleteAsync(entidad.Id);
        await LoadItems();
        Snackbar.Add("Entidad eliminada", Severity.Success);
    }
}
```

### 2. Crear un Diálogo (MudDialog)

```razor
@rendermode InteractiveServer
@using MiApp.Domain.Interfaces
@using MiApp.Domain.Entities
@inject IEntidadService EntidadService

<MudDialog>
    <DialogContent>
        <MudForm @ref="_form" @bind-IsValid="@_formIsValid">
            <MudTextField Label="Nombre"
                          @bind-Value="_model.Name"
                          Required="true"
                          RequiredError="El nombre es obligatorio"
                          MaxLength="200"
                          Counter="200"
                          Immediate="true" />
        </MudForm>
    </DialogContent>
    <DialogActions>
        <MudButton OnClick="Cancel">Cancelar</MudButton>
        <MudButton Color="Color.Primary"
                   Variant="Variant.Filled"
                   OnClick="Submit"
                   Disabled="@(!_formIsValid || _processing)">
            @if (_processing)
            {
                <MudProgressCircular Size="Size.Small" Indeterminate="true" />
                <MudText Class="ms-2">Guardando...</MudText>
            }
            else
            {
                <MudText>@(IsEditMode ? "Actualizar" : "Crear")</MudText>
            }
        </MudButton>
    </DialogActions>
</MudDialog>

@code {
    [CascadingParameter]
    MudDialogInstance MudDialog { get; set; } = null!;

    [Parameter]
    public Entidad? Entidad { get; set; }

    [Parameter]
    public bool IsEditMode { get; set; }

    private MudForm _form = null!;
    private bool _formIsValid;
    private bool _processing;
    private EntidadModel _model = new();

    protected override void OnInitialized()
    {
        if (Entidad != null)
        {
            _model.Name = Entidad.Name;
        }
    }

    private void Cancel()
    {
        MudDialog?.Close(DialogResult.Cancel());
    }

    private async Task Submit()
    {
        await _form.Validate();
        if (!_formIsValid) return;

        _processing = true;
        try
        {
            if (IsEditMode && Entidad != null)
            {
                await EntidadService.UpdateAsync(Entidad.Id, _model.Name);
            }
            else
            {
                await EntidadService.CreateAsync(_model.Name);
            }

            MudDialog?.Close(DialogResult.Ok(true));
        }
        finally
        {
            _processing = false;
        }
    }

    private class EntidadModel
    {
        public string Name { get; set; } = string.Empty;
    }
}
```

---

## 🔍 Checklist de Verificación

Antes de confirmar cambios, verifica:

- [ ] ✅ `@rendermode InteractiveServer` está en la **línea 1** de componentes interactivos
- [ ] ✅ **NO hay** `@rendermode` en `MainLayout.razor`
- [ ] ✅ Todos los `@onclick`, `@onchange` están en componentes con rendermode
- [ ] ✅ Los diálogos de MudBlazor tienen `@rendermode InteractiveServer`
- [ ] ✅ El código compila sin errores (`dotnet build`)
- [ ] ✅ Los botones responden al hacer clic (prueba en navegador)
- [ ] ✅ No hay errores en la consola del navegador (F12)

---

## 🚫 Errores Comunes a Evitar

### ❌ Error #1: Rendermode en posición incorrecta

```razor
@page "/test"
@rendermode InteractiveServer  // ❌ TARDE
```

**Solución**: Mover a la línea 1.

### ❌ Error #2: Rendermode en Layout

```razor
@inherits LayoutComponentBase
@rendermode InteractiveServer  // ❌ NUNCA en layouts
```

**Solución**: Eliminar. Los layouts NO llevan rendermode.

### ❌ Error #3: Olvidar rendermode en diálogos

```razor
@inject IDialogService DialogService

<MudDialog>
    <DialogActions>
        <MudButton OnClick="Submit">OK</MudButton>  // ❌ No funcionará
    </DialogActions>
</MudDialog>
```

**Solución**: Agregar `@rendermode InteractiveServer` en línea 1.

### ❌ Error #4: Usar eventos en Static SSR

```razor
@page "/static"
<!-- Sin @rendermode -->

<MudButton OnClick="MiMetodo">Acción</MudButton>  // ❌ No funcionará
```

**Solución**: Agregar `@rendermode InteractiveServer`.

---

## 💡 Comandos Útiles

```bash
# Restaurar dependencias
dotnet restore

# Compilar (verificar errores)
dotnet build

# Ejecutar aplicación
dotnet run

# Crear migración de EF Core
dotnet ef migrations add NombreMigracion

# Aplicar migraciones
dotnet ef database update

# Formatear código
dotnet format
```

---

## 📚 Referencias Rápidas

- **Documentación AGENTS.md**: `c:\SOURCE\zsrc\SocialPanelCore\AGENTS.md`
- **MudBlazor Docs**: https://mudblazor.com/
- **Blazor Render Modes**: https://learn.microsoft.com/en-us/aspnet/core/blazor/components/render-modes

---

## 🔄 Changelog

- **v1.0 (2025-12-10)**: Versión inicial con reglas críticas de render modes.

---

**¿Dudas?** Consulta primero este documento antes de crear componentes.
