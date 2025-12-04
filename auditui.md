# Auditoría de UI - SocialPanelCore

**Fecha:** 2025-12-04
**Versión del proyecto:** .NET 10 / Blazor con MudBlazor
**Autor:** Auditoría Automatizada

---

## Resumen Ejecutivo

Se ha realizado un análisis exhaustivo de la interfaz de usuario del proyecto SocialPanelCore. Se han identificado **problemas críticos** que impiden el funcionamiento correcto de los diálogos de MudBlazor, así como múltiples **inconsistencias de diseño** y **malas prácticas** que afectan la experiencia de usuario.

### Estadísticas del Análisis

| Categoría | Cantidad |
|-----------|----------|
| Errores Críticos | 4 |
| Errores Altos | 6 |
| Errores Medios | 8 |
| Mejoras de Diseño | 15+ |
| Total de Archivos Afectados | 40+ |

---

## PARTE 1: ERRORES CRÍTICOS - Los Diálogos No Se Abren

### 1.1 Causa Raíz del Problema

**Diagnóstico:** Los botones "Nueva Cuenta", "Nuevo Usuario", etc. no funcionan porque los componentes de diálogo no tienen declarado el modo de renderizado interactivo.

**Explicación técnica:**
- Las páginas padre (ej: `/accounts`, `/users`) tienen `@rendermode InteractiveServer`
- Los componentes de diálogo que se invocan desde estas páginas NO tienen `@rendermode`
- Cuando MudBlazor intenta mostrar el diálogo, el componente se renderiza en modo Static (SSR)
- Un componente Static no puede manejar eventos de usuario (clicks, inputs, etc.)
- **Resultado:** El diálogo aparece "muerto" o no aparece en absoluto

### 1.2 Archivos Afectados

| # | Archivo | Ruta Completa | Línea a Modificar |
|---|---------|---------------|-------------------|
| 1 | AccountDialog.razor | `/Components/Pages/Accounts/AccountDialog.razor` | Línea 1 |
| 2 | UserDialog.razor | `/Components/Pages/Users/UserDialog.razor` | Línea 1 |
| 3 | ReviewDialog.razor | `/Components/Pages/Reviews/ReviewDialog.razor` | Línea 1 |
| 4 | ConfirmDialog.razor | `/Components/Shared/Dialogs/ConfirmDialog.razor` | Línea 1 |

---

## TAREAS PARA DESARROLLADORES - SECCIÓN CRÍTICA

### TASK-001: Corregir AccountDialog.razor

**Prioridad:** CRÍTICA
**Tiempo estimado:** 5 minutos
**Archivo:** `/home/user/SocialPanelCore/Components/Pages/Accounts/AccountDialog.razor`

**Estado actual (líneas 1-4):**
```razor
@using SocialPanelCore.Domain.Interfaces
@using SocialPanelCore.Domain.Entities
@inject IAccountService AccountService

<MudDialog>
```

**Cambio requerido:**
Agregar `@rendermode InteractiveServer` como segunda línea del archivo.

**Estado final (líneas 1-5):**
```razor
@using SocialPanelCore.Domain.Interfaces
@rendermode InteractiveServer
@using SocialPanelCore.Domain.Entities
@inject IAccountService AccountService

<MudDialog>
```

**Pasos detallados:**
1. Abrir el archivo `Components/Pages/Accounts/AccountDialog.razor`
2. Posicionar el cursor después de la primera línea `@using SocialPanelCore.Domain.Interfaces`
3. Insertar una nueva línea
4. Escribir exactamente: `@rendermode InteractiveServer`
5. Guardar el archivo
6. Verificar que no hay errores de compilación

**Verificación:**
- Ejecutar la aplicación
- Navegar a `/accounts`
- Hacer clic en "Nueva Cuenta"
- El diálogo debe abrirse correctamente

---

### TASK-002: Corregir UserDialog.razor

**Prioridad:** CRÍTICA
**Tiempo estimado:** 5 minutos
**Archivo:** `/home/user/SocialPanelCore/Components/Pages/Users/UserDialog.razor`

**Estado actual (líneas 1-4):**
```razor
@using SocialPanelCore.Domain.Interfaces
@using SocialPanelCore.Domain.Entities
@using SocialPanelCore.Domain.Enums
@inject IUserService UserService
```

**Cambio requerido:**
Agregar `@rendermode InteractiveServer` después de los `@using`.

**Estado final (líneas 1-5):**
```razor
@using SocialPanelCore.Domain.Interfaces
@using SocialPanelCore.Domain.Entities
@using SocialPanelCore.Domain.Enums
@rendermode InteractiveServer
@inject IUserService UserService
```

**Pasos detallados:**
1. Abrir el archivo `Components/Pages/Users/UserDialog.razor`
2. Localizar la línea `@inject IUserService UserService`
3. Insertar una línea ANTES de esa línea
4. Escribir exactamente: `@rendermode InteractiveServer`
5. Guardar el archivo

**Verificación:**
- Navegar a `/users` (requiere rol Superadministrador)
- Hacer clic en "Nuevo Usuario"
- El diálogo debe abrirse y permitir interacción

---

### TASK-003: Corregir ReviewDialog.razor

**Prioridad:** CRÍTICA
**Tiempo estimado:** 5 minutos
**Archivo:** `/home/user/SocialPanelCore/Components/Pages/Reviews/ReviewDialog.razor`

**Estado actual (líneas 1-3):**
```razor
@using SocialPanelCore.Domain.Interfaces
@inject IBasePostService BasePostService
@inject AuthenticationStateProvider AuthenticationStateProvider
```

**Cambio requerido:**
Agregar `@rendermode InteractiveServer` después del `@using`.

**Estado final (líneas 1-4):**
```razor
@using SocialPanelCore.Domain.Interfaces
@rendermode InteractiveServer
@inject IBasePostService BasePostService
@inject AuthenticationStateProvider AuthenticationStateProvider
```

**Pasos detallados:**
1. Abrir el archivo `Components/Pages/Reviews/ReviewDialog.razor`
2. Insertar `@rendermode InteractiveServer` después de la línea 1
3. Guardar el archivo

---

### TASK-004: Corregir ConfirmDialog.razor

**Prioridad:** CRÍTICA
**Tiempo estimado:** 5 minutos
**Archivo:** `/home/user/SocialPanelCore/Components/Shared/Dialogs/ConfirmDialog.razor`

**Estado actual (líneas 1-2):**
```razor
<MudDialog>
    <DialogContent>
```

**Cambio requerido:**
Agregar `@rendermode InteractiveServer` como primera línea.

**Estado final (líneas 1-3):**
```razor
@rendermode InteractiveServer
<MudDialog>
    <DialogContent>
```

**Pasos detallados:**
1. Abrir el archivo `Components/Shared/Dialogs/ConfirmDialog.razor`
2. Insertar una nueva primera línea
3. Escribir: `@rendermode InteractiveServer`
4. Guardar el archivo

**Verificación:**
- Navegar a `/accounts`
- Hacer clic en el icono de eliminar (papelera) de cualquier cuenta
- El diálogo de confirmación debe aparecer

---

## PARTE 2: ERRORES DE INCONSISTENCIA EN RENDERMODE

### 2.1 Descripción del Problema

El proyecto tiene una mezcla inconsistente de componentes con y sin `@rendermode`. Esto puede causar comportamientos impredecibles y problemas de interactividad.

### 2.2 Archivos Sin @rendermode que Deberían Tenerlo

| # | Archivo | Consecuencia |
|---|---------|--------------|
| 1 | `MainLayout.razor` | El layout no es interactivo por defecto |
| 2 | `NavMenu.razor` | Los eventos de click pueden no funcionar |
| 3 | `Home.razor` | Dashboard sin interactividad |

---

### TASK-005: Agregar @rendermode a Home.razor

**Prioridad:** ALTA
**Tiempo estimado:** 5 minutos
**Archivo:** `/home/user/SocialPanelCore/Components/Pages/Home.razor`

**Problema:** La página de Dashboard no tiene modo interactivo, lo que impide futuras interacciones dinámicas.

**Estado actual (líneas 1-2):**
```razor
@page "/"
@using SocialPanelCore.Domain.Interfaces
```

**Cambio requerido:**
```razor
@page "/"
@rendermode InteractiveServer
@using SocialPanelCore.Domain.Interfaces
```

**Pasos detallados:**
1. Abrir `Components/Pages/Home.razor`
2. Agregar `@rendermode InteractiveServer` después de `@page "/"`
3. Guardar

---

### TASK-006: Revisar consistencia de NavMenu.razor

**Prioridad:** ALTA
**Tiempo estimado:** 10 minutos
**Archivo:** `/home/user/SocialPanelCore/Components/Layout/NavMenu.razor`

**Problema actual:** El NavMenu tiene un método `LogoutClick()` que usa `NavigationManager.NavigateTo()`. Aunque funciona porque se hereda el rendermode del layout padre, es mejor ser explícito.

**Estado actual (línea 1):**
```razor
@inject NavigationManager NavigationManager
```

**Cambio requerido:**
```razor
@rendermode InteractiveServer
@inject NavigationManager NavigationManager
```

**Verificación:**
- El botón "Cerrar Sesión" debe funcionar correctamente
- Los enlaces de navegación deben responder al click

---

## PARTE 3: PÁGINAS DE AUTENTICACIÓN - DISEÑO DEFICIENTE

### 3.1 Descripción General del Problema

Las páginas de autenticación (`Login`, `Register`, `ForgotPassword`, etc.) presentan múltiples problemas:

1. **Inconsistencia de framework:** Usan Bootstrap mientras el resto de la app usa MudBlazor
2. **Textos en inglés:** El resto de la app está en español
3. **Diseño pobre:** Sin centrado, sin logo, sin branding
4. **Falta de componentes MudBlazor:** No usan `MudTextField`, `MudButton`, etc.
5. **Sin feedback visual:** No tienen estados de carga ni animaciones

### 3.2 Inventario de Páginas Afectadas

| # | Página | Ruta | Problemas Principales |
|---|--------|------|----------------------|
| 1 | Login.razor | `/Account/Login` | Bootstrap, inglés, sin centrado |
| 2 | Register.razor | `/Account/Register` | Bootstrap, inglés, sin centrado |
| 3 | ForgotPassword.razor | `/Account/ForgotPassword` | Minimalista, inglés |
| 4 | ResetPassword.razor | `/Account/ResetPassword` | Minimalista, inglés |
| 5 | Lockout.razor | `/Account/Lockout` | Solo texto plano, sin diseño |
| 6 | AccessDenied.razor | `/Account/AccessDenied` | Solo texto plano, sin diseño |
| 7 | RegisterConfirmation.razor | `/Account/RegisterConfirmation` | Sin diseño, texto técnico |
| 8 | ResendEmailConfirmation.razor | `/Account/ResendEmailConfirmation` | Bootstrap, minimalista |
| 9 | ConfirmEmail.razor | `/Account/ConfirmEmail` | Sin diseño |
| 10 | ForgotPasswordConfirmation.razor | `/Account/ForgotPasswordConfirmation` | Sin diseño |

---

### TASK-007: Rediseñar Login.razor con MudBlazor

**Prioridad:** ALTA
**Tiempo estimado:** 45-60 minutos
**Archivo:** `/home/user/SocialPanelCore/Components/Account/Pages/Login.razor`

**Problemas actuales identificados:**

1. **Línea 14:** `<PageTitle>Log in</PageTitle>` - Título en inglés
2. **Línea 16:** `<h1>Log in</h1>` - Encabezado en inglés
3. **Línea 23:** `<h2>Use a local account to log in.</h2>` - Texto en inglés
4. **Líneas 26-34:** Usa `<InputText>` con clases Bootstrap en lugar de `<MudTextField>`
5. **Línea 43:** `<button class="btn btn-lg btn-primary">` - Botón Bootstrap
6. **Líneas 17-72:** Layout en columnas Bootstrap sin centrado
7. **Línea 47:** Texto "OR" en inglés
8. **Líneas 53-60:** Enlaces sin estilo MudBlazor

**Cambios requeridos - Paso a paso:**

**Paso 1:** Cambiar el título y encabezados a español
```razor
<!-- ANTES -->
<PageTitle>Log in</PageTitle>
<h1>Log in</h1>
<h2>Use a local account to log in.</h2>

<!-- DESPUÉS -->
<PageTitle>Iniciar Sesión</PageTitle>
<MudText Typo="Typo.h4" Class="mb-4">Iniciar Sesión</MudText>
<MudText Typo="Typo.subtitle1" Class="mb-4">Ingresa con tu cuenta local</MudText>
```

**Paso 2:** Envolver todo en un contenedor centrado
```razor
<!-- ESTRUCTURA RECOMENDADA -->
<MudContainer MaxWidth="MaxWidth.Small" Class="mt-8">
    <MudPaper Elevation="4" Class="pa-8">
        <div class="d-flex flex-column align-center mb-4">
            <MudIcon Icon="@Icons.Material.Filled.Lock" Size="Size.Large" Color="Color.Primary" />
            <MudText Typo="Typo.h4" Class="mt-2">Iniciar Sesión</MudText>
        </div>

        <!-- Contenido del formulario aquí -->

    </MudPaper>
</MudContainer>
```

**Paso 3:** Reemplazar campos de Bootstrap por MudBlazor
```razor
<!-- ANTES (Bootstrap) -->
<div class="form-floating mb-3">
    <InputText @bind-Value="Input.Email" class="form-control" ... />
    <label>Email</label>
</div>

<!-- DESPUÉS (MudBlazor) -->
<MudTextField @bind-Value="Input.Email"
              Label="Correo electrónico"
              Variant="Variant.Outlined"
              InputType="InputType.Email"
              Required="true"
              RequiredError="El correo es obligatorio"
              Class="mb-4" />
```

**Paso 4:** Reemplazar el botón
```razor
<!-- ANTES -->
<button type="submit" class="w-100 btn btn-lg btn-primary">Log in</button>

<!-- DESPUÉS -->
<MudButton ButtonType="ButtonType.Submit"
           Variant="Variant.Filled"
           Color="Color.Primary"
           FullWidth="true"
           Size="Size.Large"
           Class="mb-4">
    Iniciar Sesión
</MudButton>
```

**Paso 5:** Reemplazar el checkbox
```razor
<!-- ANTES -->
<div class="checkbox mb-3">
    <label>
        <InputCheckbox @bind-Value="Input.RememberMe" class="darker-border-checkbox form-check-input" />
        Remember me
    </label>
</div>

<!-- DESPUÉS -->
<MudCheckBox @bind-Value="Input.RememberMe"
             Label="Recordarme"
             Color="Color.Primary"
             Class="mb-4" />
```

**Paso 6:** Reemplazar los enlaces
```razor
<!-- ANTES -->
<p><a href="Account/ForgotPassword">Forgot your password?</a></p>
<p><a href="...">Register as a new user</a></p>

<!-- DESPUÉS -->
<div class="d-flex flex-column align-center">
    <MudLink Href="Account/ForgotPassword" Typo="Typo.body2">
        ¿Olvidaste tu contraseña?
    </MudLink>
    <MudLink Href="@registerUrl" Typo="Typo.body2" Class="mt-2">
        ¿No tienes cuenta? Regístrate
    </MudLink>
</div>
```

**Verificación:**
1. La página debe verse centrada en pantalla
2. Los campos deben tener el estilo de MudBlazor
3. Todos los textos deben estar en español
4. El formulario debe seguir funcionando (enviar credenciales)

---

### TASK-008: Rediseñar Register.razor con MudBlazor

**Prioridad:** ALTA
**Tiempo estimado:** 45-60 minutos
**Archivo:** `/home/user/SocialPanelCore/Components/Account/Pages/Register.razor`

**Problemas identificados:**

1. **Línea 18:** `<PageTitle>Register</PageTitle>` - Inglés
2. **Línea 20:** `<h1>Register</h1>` - Inglés
3. **Línea 27:** `<h2>Create a new account.</h2>` - Inglés
4. **Líneas 30-44:** Campos Bootstrap
5. **Línea 45:** Botón Bootstrap
6. **Línea 50:** `<h3>Use another service to register.</h3>` - Inglés

**Cambios requeridos:**

**Estructura recomendada:**
```razor
<MudContainer MaxWidth="MaxWidth.Small" Class="mt-8">
    <MudPaper Elevation="4" Class="pa-8">
        <div class="d-flex flex-column align-center mb-4">
            <MudIcon Icon="@Icons.Material.Filled.PersonAdd" Size="Size.Large" Color="Color.Primary" />
            <MudText Typo="Typo.h4" Class="mt-2">Crear Cuenta</MudText>
            <MudText Typo="Typo.subtitle1" Color="Color.Secondary">
                Regístrate para comenzar
            </MudText>
        </div>

        <EditForm Model="Input" method="post" OnValidSubmit="RegisterUser" FormName="register">
            <DataAnnotationsValidator />

            <MudTextField @bind-Value="Input.Email"
                          Label="Correo electrónico"
                          Variant="Variant.Outlined"
                          InputType="InputType.Email"
                          Required="true"
                          Class="mb-4" />

            <MudTextField @bind-Value="Input.Password"
                          Label="Contraseña"
                          Variant="Variant.Outlined"
                          InputType="InputType.Password"
                          Required="true"
                          Class="mb-4" />

            <MudTextField @bind-Value="Input.ConfirmPassword"
                          Label="Confirmar contraseña"
                          Variant="Variant.Outlined"
                          InputType="InputType.Password"
                          Required="true"
                          Class="mb-4" />

            <MudButton ButtonType="ButtonType.Submit"
                       Variant="Variant.Filled"
                       Color="Color.Primary"
                       FullWidth="true"
                       Size="Size.Large">
                Registrarse
            </MudButton>
        </EditForm>

        <MudDivider Class="my-4" />

        <div class="d-flex justify-center">
            <MudLink Href="Account/Login" Typo="Typo.body2">
                ¿Ya tienes cuenta? Inicia sesión
            </MudLink>
        </div>
    </MudPaper>
</MudContainer>
```

---

### TASK-009: Rediseñar ForgotPassword.razor

**Prioridad:** MEDIA
**Tiempo estimado:** 30 minutos
**Archivo:** `/home/user/SocialPanelCore/Components/Account/Pages/ForgotPassword.razor`

**Problemas identificados:**

1. **Línea 15:** `<PageTitle>Forgot your password?</PageTitle>` - Inglés
2. **Línea 17-18:** Encabezados en inglés
3. **Línea 31:** `Reset password` en el botón - Inglés
4. **Sin diseño centrado**
5. **Sin icono o indicación visual**

**Estructura recomendada:**
```razor
<MudContainer MaxWidth="MaxWidth.Small" Class="mt-8">
    <MudPaper Elevation="4" Class="pa-8">
        <div class="d-flex flex-column align-center mb-4">
            <MudIcon Icon="@Icons.Material.Filled.LockReset" Size="Size.Large" Color="Color.Warning" />
            <MudText Typo="Typo.h4" Class="mt-2">Recuperar Contraseña</MudText>
            <MudText Typo="Typo.body1" Color="Color.Secondary" Align="Align.Center">
                Ingresa tu correo electrónico y te enviaremos un enlace para restablecer tu contraseña.
            </MudText>
        </div>

        <!-- Formulario con MudTextField -->

    </MudPaper>
</MudContainer>
```

---

### TASK-010: Rediseñar ResetPassword.razor

**Prioridad:** MEDIA
**Tiempo estimado:** 30 minutos
**Archivo:** `/home/user/SocialPanelCore/Components/Account/Pages/ResetPassword.razor`

**Problemas identificados:**

1. **Línea 12-14:** Títulos en inglés
2. **Campos con estilo Bootstrap**
3. **Botón con texto "Reset"**
4. **Sin diseño centrado**

**Traducciones requeridas:**
- "Reset password" → "Restablecer Contraseña"
- "Reset your password." → "Crea una nueva contraseña"
- "Reset" (botón) → "Restablecer"

---

### TASK-011: Rediseñar Lockout.razor

**Prioridad:** MEDIA
**Tiempo estimado:** 20 minutos
**Archivo:** `/home/user/SocialPanelCore/Components/Account/Pages/Lockout.razor`

**Estado actual (todo el archivo):**
```razor
@page "/Account/Lockout"

<PageTitle>Locked out</PageTitle>

<header>
    <h1 class="text-danger">Locked out</h1>
    <p class="text-danger" role="alert">This account has been locked out, please try again later.</p>
</header>
```

**Problemas:**
- Todo en inglés
- Diseño extremadamente pobre
- Sin componentes MudBlazor
- Sin indicación de tiempo de espera
- Sin enlace para volver

**Rediseño recomendado:**
```razor
@page "/Account/Lockout"

<PageTitle>Cuenta Bloqueada</PageTitle>

<MudContainer MaxWidth="MaxWidth.Small" Class="mt-16">
    <MudPaper Elevation="4" Class="pa-8">
        <div class="d-flex flex-column align-center">
            <MudIcon Icon="@Icons.Material.Filled.Lock"
                     Size="Size.Large"
                     Color="Color.Error"
                     Class="mb-4" />

            <MudText Typo="Typo.h4" Color="Color.Error" Class="mb-2">
                Cuenta Bloqueada
            </MudText>

            <MudText Typo="Typo.body1" Align="Align.Center" Class="mb-4">
                Tu cuenta ha sido bloqueada temporalmente debido a múltiples
                intentos de inicio de sesión fallidos.
            </MudText>

            <MudAlert Severity="Severity.Warning" Class="mb-4">
                Por favor, espera unos minutos antes de intentar nuevamente.
            </MudAlert>

            <MudButton Href="Account/Login"
                       Variant="Variant.Outlined"
                       Color="Color.Primary">
                Volver al inicio de sesión
            </MudButton>
        </div>
    </MudPaper>
</MudContainer>
```

---

### TASK-012: Rediseñar AccessDenied.razor

**Prioridad:** MEDIA
**Tiempo estimado:** 20 minutos
**Archivo:** `/home/user/SocialPanelCore/Components/Account/Pages/AccessDenied.razor`

**Estado actual:**
```razor
@page "/Account/AccessDenied"

<PageTitle>Access denied</PageTitle>

<header>
    <h1 class="text-danger">Access denied</h1>
    <p class="text-danger">You do not have access to this resource.</p>
</header>
```

**Rediseño recomendado:**
```razor
@page "/Account/AccessDenied"

<PageTitle>Acceso Denegado</PageTitle>

<MudContainer MaxWidth="MaxWidth.Small" Class="mt-16">
    <MudPaper Elevation="4" Class="pa-8">
        <div class="d-flex flex-column align-center">
            <MudIcon Icon="@Icons.Material.Filled.Block"
                     Size="Size.Large"
                     Color="Color.Error"
                     Class="mb-4" />

            <MudText Typo="Typo.h4" Color="Color.Error" Class="mb-2">
                Acceso Denegado
            </MudText>

            <MudText Typo="Typo.body1" Align="Align.Center" Class="mb-4">
                No tienes permisos para acceder a este recurso.
            </MudText>

            <MudStack Row="true" Spacing="2">
                <MudButton Href="/"
                           Variant="Variant.Filled"
                           Color="Color.Primary"
                           StartIcon="@Icons.Material.Filled.Home">
                    Ir al Inicio
                </MudButton>
                <MudButton Href="Account/Login"
                           Variant="Variant.Outlined"
                           Color="Color.Primary">
                    Iniciar con otra cuenta
                </MudButton>
            </MudStack>
        </div>
    </MudPaper>
</MudContainer>
```

---

### TASK-013: Traducir y mejorar RegisterConfirmation.razor

**Prioridad:** MEDIA
**Tiempo estimado:** 25 minutos
**Archivo:** `/home/user/SocialPanelCore/Components/Account/Pages/RegisterConfirmation.razor`

**Problemas identificados:**

1. **Línea 13:** Título en inglés
2. **Línea 15:** Encabezado en inglés
3. **Líneas 21-24:** Texto técnico en inglés sobre email sender
4. **Línea 28:** Mensaje en inglés
5. **Sin diseño visual atractivo**

**Traducciones:**
- "Register confirmation" → "Confirmación de Registro"
- "Please check your email to confirm your account." → "Por favor revisa tu correo electrónico para confirmar tu cuenta."

---

### TASK-014: Traducir ResendEmailConfirmation.razor

**Prioridad:** BAJA
**Tiempo estimado:** 20 minutos
**Archivo:** `/home/user/SocialPanelCore/Components/Account/Pages/ResendEmailConfirmation.razor`

**Traducciones requeridas:**
- "Resend email confirmation" → "Reenviar confirmación de correo"
- "Enter your email." → "Ingresa tu correo electrónico"
- "Resend" (botón) → "Reenviar"

---

## PARTE 4: COMPONENTES SHARED - MEJORAS NECESARIAS

### TASK-015: Mejorar StatusMessage.razor

**Prioridad:** MEDIA
**Tiempo estimado:** 15 minutos
**Archivo:** `/home/user/SocialPanelCore/Components/Account/Shared/StatusMessage.razor`

**Estado actual:**
```razor
@if (!string.IsNullOrEmpty(DisplayMessage))
{
    var statusMessageClass = DisplayMessage.StartsWith("Error") ? "danger" : "success";
    <div class="alert alert-@statusMessageClass" role="alert">
        @DisplayMessage
    </div>
}
```

**Problema:** Usa alertas Bootstrap en lugar de MudBlazor.

**Cambio recomendado:**
```razor
@if (!string.IsNullOrEmpty(DisplayMessage))
{
    var severity = DisplayMessage.StartsWith("Error") ? Severity.Error : Severity.Success;
    <MudAlert Severity="@severity" Class="mb-4" ShowCloseIcon="true">
        @DisplayMessage
    </MudAlert>
}
```

---

### TASK-016: Mejorar ManageLayout.razor

**Prioridad:** MEDIA
**Tiempo estimado:** 30 minutos
**Archivo:** `/home/user/SocialPanelCore/Components/Account/Shared/ManageLayout.razor`

**Estado actual:**
```razor
@inherits LayoutComponentBase
@layout SocialPanelCore.Components.Layout.MainLayout

<h1>Manage your account</h1>

<div>
    <h2>Change your account settings</h2>
    <hr />
    <div class="row">
        <div class="col-lg-3">
            <ManageNavMenu />
        </div>
        <div class="col-lg-9">
            @Body
        </div>
    </div>
</div>
```

**Problemas:**
- Textos en inglés
- Usa clases Bootstrap (row, col-lg-*)
- Sin componentes MudBlazor

**Cambio recomendado:**
```razor
@inherits LayoutComponentBase
@layout SocialPanelCore.Components.Layout.MainLayout

<MudText Typo="Typo.h4" Class="mb-2">Mi Cuenta</MudText>
<MudText Typo="Typo.subtitle1" Color="Color.Secondary" Class="mb-4">
    Administra tu configuración de cuenta
</MudText>

<MudDivider Class="mb-4" />

<MudGrid>
    <MudItem xs="12" md="3">
        <ManageNavMenu />
    </MudItem>
    <MudItem xs="12" md="9">
        @Body
    </MudItem>
</MudGrid>
```

---

## PARTE 5: OTRAS PÁGINAS DE ACCOUNT A REVISAR

### 5.1 Lista de Páginas Pendientes de Traducción/Rediseño

| # | Archivo | Prioridad | Tarea |
|---|---------|-----------|-------|
| 1 | ConfirmEmail.razor | BAJA | Traducir y agregar diseño MudBlazor |
| 2 | ConfirmEmailChange.razor | BAJA | Traducir y agregar diseño MudBlazor |
| 3 | ExternalLogin.razor | BAJA | Traducir textos |
| 4 | ForgotPasswordConfirmation.razor | BAJA | Traducir y mejorar diseño |
| 5 | InvalidPasswordReset.razor | BAJA | Traducir y mejorar diseño |
| 6 | InvalidUser.razor | BAJA | Traducir y mejorar diseño |
| 7 | LoginWith2fa.razor | MEDIA | Traducir y convertir a MudBlazor |
| 8 | LoginWithRecoveryCode.razor | BAJA | Traducir y convertir a MudBlazor |
| 9 | ResetPasswordConfirmation.razor | BAJA | Traducir y mejorar diseño |

### 5.2 Páginas de Manage (Gestión de Perfil)

| # | Archivo | Ruta | Prioridad |
|---|---------|------|-----------|
| 1 | Manage/Index.razor | `/Account/Manage` | MEDIA |
| 2 | Manage/ChangePassword.razor | `/Account/Manage/ChangePassword` | MEDIA |
| 3 | Manage/Email.razor | `/Account/Manage/Email` | MEDIA |
| 4 | Manage/DeletePersonalData.razor | `/Account/Manage/DeletePersonalData` | BAJA |
| 5 | Manage/Disable2fa.razor | `/Account/Manage/Disable2fa` | BAJA |
| 6 | Manage/EnableAuthenticator.razor | `/Account/Manage/EnableAuthenticator` | BAJA |
| 7 | Manage/ExternalLogins.razor | `/Account/Manage/ExternalLogins` | BAJA |
| 8 | Manage/GenerateRecoveryCodes.razor | `/Account/Manage/GenerateRecoveryCodes` | BAJA |
| 9 | Manage/Passkeys.razor | `/Account/Manage/Passkeys` | BAJA |
| 10 | Manage/PersonalData.razor | `/Account/Manage/PersonalData` | BAJA |
| 11 | Manage/ResetAuthenticator.razor | `/Account/Manage/ResetAuthenticator` | BAJA |
| 12 | Manage/SetPassword.razor | `/Account/Manage/SetPassword` | BAJA |
| 13 | Manage/TwoFactorAuthentication.razor | `/Account/Manage/TwoFactorAuthentication` | BAJA |

---

## PARTE 6: INCONSISTENCIAS DE CSS

### TASK-017: Revisar conflictos CSS Bootstrap vs MudBlazor

**Prioridad:** MEDIA
**Tiempo estimado:** 30 minutos
**Archivo:** `/home/user/SocialPanelCore/wwwroot/app.css`

**Estado actual:** El archivo contiene estilos Bootstrap que pueden entrar en conflicto con MudBlazor.

**Contenido actual relevante:**
```css
html, body {
    font-family: 'Helvetica Neue', Helvetica, Arial, sans-serif;
}

.btn-primary {
    color: #fff;
    background-color: #1b6ec2;
    border-color: #1861ac;
}
```

**Problemas:**
1. La fuente definida sobrescribe la fuente Roboto de MudBlazor
2. Los estilos de `.btn-primary` pueden interferir con componentes híbridos
3. Estilos de validación personalizados pueden chocar con MudBlazor

**Cambios recomendados:**

1. Eliminar la sobrescritura de fuente:
```css
/* ELIMINAR o comentar esto: */
/* html, body {
    font-family: 'Helvetica Neue', Helvetica, Arial, sans-serif;
} */
```

2. Agregar scope a los estilos Bootstrap (solo para páginas Account):
```css
/* Estilos solo para páginas de Account que aún usan Bootstrap */
.account-page .btn-primary {
    color: #fff;
    background-color: #1b6ec2;
    border-color: #1861ac;
}
```

---

## PARTE 7: MEJORES PRÁCTICAS NO IMPLEMENTADAS

### 7.1 Falta de Loading States

**Problema:** Varias páginas no muestran indicadores de carga mientras obtienen datos.

### TASK-018: Agregar loading state a Home.razor

**Prioridad:** BAJA
**Tiempo estimado:** 15 minutos
**Archivo:** `/home/user/SocialPanelCore/Components/Pages/Home.razor`

**Cambio requerido:**
Agregar una variable `_loading` y mostrar un skeleton mientras carga:

```razor
@if (_loading)
{
    <MudGrid>
        <MudItem xs="12" sm="6" md="3">
            <MudSkeleton SkeletonType="SkeletonType.Rectangle" Height="100px" />
        </MudItem>
        <!-- Repetir para cada tarjeta -->
    </MudGrid>
}
else
{
    <!-- Contenido actual -->
}

@code {
    private bool _loading = true;

    protected override async Task OnInitializedAsync()
    {
        await LoadDashboardData();
        _loading = false;
    }
}
```

---

### 7.2 Falta de Manejo de Errores Visual

**Problema:** Cuando hay errores de carga, no se muestra feedback al usuario.

### TASK-019: Agregar manejo de errores visual

**Prioridad:** BAJA
**Tiempo estimado:** 20 minutos

**Ejemplo de implementación:**
```razor
@if (_error != null)
{
    <MudAlert Severity="Severity.Error" Class="mb-4">
        @_error
        <MudButton OnClick="Retry" Variant="Variant.Text" Color="Color.Error">
            Reintentar
        </MudButton>
    </MudAlert>
}
```

---

## PARTE 8: CHECKLIST DE VERIFICACIÓN

### 8.1 Después de completar las tareas críticas (TASK-001 a TASK-004):

- [ ] Los diálogos se abren al hacer clic en "Nueva Cuenta"
- [ ] Los diálogos se abren al hacer clic en "Nuevo Usuario"
- [ ] Los diálogos permiten escribir en los campos de texto
- [ ] Los botones "Cancelar" y "Crear/Actualizar" funcionan
- [ ] El diálogo de confirmación de eliminación aparece
- [ ] No hay errores en la consola del navegador (F12)

### 8.2 Después de completar las tareas de autenticación (TASK-007 a TASK-014):

- [ ] La página de Login está centrada y usa MudBlazor
- [ ] Todos los textos están en español
- [ ] Los campos de formulario tienen el estilo MudBlazor
- [ ] Los botones tienen el estilo MudBlazor
- [ ] La página de Register sigue el mismo estilo que Login
- [ ] Las páginas de error (Lockout, AccessDenied) son visualmente atractivas
- [ ] El login/registro sigue funcionando correctamente

### 8.3 Verificación general:

- [ ] No hay errores de compilación
- [ ] La aplicación inicia correctamente
- [ ] La navegación funciona en todas las páginas
- [ ] No hay estilos Bootstrap rotos visibles
- [ ] Los snackbars de notificación aparecen

---

## ANEXO A: Referencia Rápida de Componentes MudBlazor

### A.1 Reemplazos Bootstrap → MudBlazor

| Bootstrap | MudBlazor |
|-----------|-----------|
| `<button class="btn btn-primary">` | `<MudButton Variant="Variant.Filled" Color="Color.Primary">` |
| `<input class="form-control">` | `<MudTextField>` |
| `<div class="alert alert-danger">` | `<MudAlert Severity="Severity.Error">` |
| `<div class="row">` | `<MudGrid>` |
| `<div class="col-md-6">` | `<MudItem xs="12" md="6">` |
| `<div class="card">` | `<MudCard>` |
| `<hr />` | `<MudDivider>` |
| `<a href="...">` | `<MudLink Href="...">` |

### A.2 Importaciones Necesarias

Asegurarse que `_Imports.razor` contiene:
```razor
@using MudBlazor
```

(Ya está presente en el proyecto - línea 15)

---

## ANEXO B: Orden de Prioridad de Tareas

### Fase 1: Correcciones Críticas (Día 1)
1. TASK-001: AccountDialog.razor
2. TASK-002: UserDialog.razor
3. TASK-003: ReviewDialog.razor
4. TASK-004: ConfirmDialog.razor

### Fase 2: Correcciones Altas (Día 2-3)
5. TASK-005: Home.razor rendermode
6. TASK-006: NavMenu.razor rendermode
7. TASK-007: Rediseño Login.razor
8. TASK-008: Rediseño Register.razor

### Fase 3: Correcciones Medias (Día 4-5)
9. TASK-009: ForgotPassword.razor
10. TASK-010: ResetPassword.razor
11. TASK-011: Lockout.razor
12. TASK-012: AccessDenied.razor

### Fase 4: Mejoras Adicionales (Semana 2)
13. TASK-013 a TASK-019

---

## Notas Finales

1. **Backup:** Antes de realizar cualquier cambio, asegurarse de tener un commit de respaldo.

2. **Testing:** Después de cada tarea, probar la funcionalidad afectada.

3. **Consistencia:** Mantener el mismo estilo visual en todas las páginas modificadas.

4. **Documentación:** Si se encuentran problemas adicionales durante la implementación, documentarlos.

---

*Documento generado automáticamente - Auditoría UI SocialPanelCore*
