# 🎯 Plantilla de Proyecto - Contexto para IA

> Documento generado para proporcionar contexto completo a sistemas de desarrollo asistido por IA.

## 📋 Stack Tecnológico Principal

**Tipo de Proyecto**: Aplicación Web con Blazor

## 🏗️ Arquitectura y Características

### Tecnología

*Tecnología usada para el desarrollo de la aplicación.*

- **Web App .NET (Blazor)**: Aplicación web con Blazor.

### Configuración esencial

*Framework, base de datos, ORM, autenticación, logging y testing.*

- **.NET 10.0**: Target framework net10.0.
- **PostgreSQL**: Base de datos relacional principal.
- **OpenRouter (Kimi k2 free)**: Proveedor de IA para generación de contenido adaptado.

### Validación

*Estrategias de validación en servidor/cliente e integración con el framework.*

- **FluentValidation**: Validación fluida con reglas avanzadas.

### Arquitectura

*Plantillas y patrones de arquitectura para el backend.*

- **Clean Architecture**: Capas Api/Application/Domain/Infrastructure.
- **Repository**: Abstracción del acceso a datos.
- **Unit of Work**: Transacciones consistentes por operación.

### ORM y datos

*Migrations, seed y gestión de datos.*

- **EF Core Migrations**: Evolución del esquema vía migraciones.

### Autenticación

*Métodos de autenticación y gestión de identidad.*

- **ASP.NET Identity**: Gestión de usuarios local; puede emitir JWT.

### Seguridad y resiliencia

*Buenas prácticas de seguridad, CORS, rate limiting y resiliencia.*

- **Polly en HttpClient**: Reintentos, timeouts, circuit breaker.

### Observabilidad

*Logging, métricas, trazas, health checks y correlación.*

- **Serilog (JSON)**: Logs estructurados para correlación.

### Procesos en background

*Servicios alojados para automatización de tareas.*

- **AdaptationHostedService**: Generación de variantes por IA (cada 180 minutos).
- **PublishingHostedService**: Publicación automática en redes sociales (cada 60 minutos).

### Visualización (Web App .NET)

*Plantilla Blazor, render modes, estilos y extras de UI.*

- **Blazor United/Auto**: Render mode automático (SSR + InteractiveServer + InteractiveWebAssembly).
- **MudBlazor**: Librería de componentes para Blazor.
- **Almacenamiento de medios**: Sistema de archivos local en wwwroot/mediavault.

#### 🎯 Reglas Críticas de Render Modes en Blazor

**IMPORTANTE**: El modo de renderizado predeterminado es **Static SSR** (Server-Side Rendering estático).

**Limitaciones de Static SSR:**
- ❌ **NO soporta eventos `@onclick`**, `@onchange`, `@oninput`, etc.
- ❌ **NO soporta interactividad en tiempo real** (JavaScript interop limitado).
- ❌ **NO puede usar diálogos de MudBlazor** que dependan de eventos.

**Cuándo usar `@rendermode InteractiveServer`:**
- ✅ Páginas con **botones que ejecutan acciones** (`@onclick`).
- ✅ Páginas con **formularios interactivos** (validación en tiempo real).
- ✅ Páginas que usan **MudDialog**, **MudDrawer**, **MudMenu** con eventos.
- ✅ Componentes que requieren **estado del lado del servidor** (SignalR).

**Cuándo usar `@rendermode InteractiveWebAssembly`:**
- ✅ Páginas con **lógica intensiva del lado del cliente** (validación compleja, gráficos).
- ✅ Componentes que deben **funcionar offline**.
- ✅ Aplicaciones que priorizan **latencia mínima** en interacciones.

**Cuándo usar `@rendermode InteractiveAuto`:**
- ✅ **Híbrido**: Primera carga con Server, luego cambia a WebAssembly cuando está disponible.
- ✅ Ideal para **aplicaciones progresivas** (PWA).

**Sintaxis obligatoria al crear nuevas páginas/componentes:**

```razor
@page "/mi-ruta"
@rendermode InteractiveServer  // 👈 SI la página tiene @onclick o eventos
@using MiNamespace

<MudButton OnClick="MiMetodo">Acción</MudButton>  <!-- ✅ Funciona -->

@code {
    private void MiMetodo() { /* ... */ }
}
```

**Sin rendermode interactivo:**
```razor
@page "/mi-ruta-estatica"
<!-- ❌ ESTO NO FUNCIONARÁ -->
<MudButton OnClick="MiMetodo">Acción</MudButton>  
```

**Restricciones de RenderFragments:**
- ❌ **NO puedes pasar `RenderFragment` entre componentes con diferentes rendermodes** (ej. `Body` en Layout).
- ✅ Aplica rendermode **solo en páginas**, **no en Layouts** (`MainLayout.razor`).

**Configuración global recomendada:**
- En `App.razor`: `@rendermode="InteractiveAuto"` en `<Routes>` y `<HeadOutlet>`.
- En páginas individuales: `@rendermode InteractiveServer` según necesidad.

### Instrucciones para la IA

*Reglas de autonomía, acciones permitidas y flujo de trabajo del desarrollo asistido por IA.*

- **IA: Acciones permitidas**: La IA puede crear/modificar código de aplicación, tests y documentación dentro de este repositorio respetando las capas definidas.
- **IA: Comandos recomendados**: La IA puede ejecutar comandos estándar de .NET (dotnet restore, dotnet build, dotnet test, dotnet format) para validar sus cambios.
- **IA: Explicación de cambios**: La IA debe acompañar los cambios con un resumen de qué ha hecho, por qué y cómo comprobarlo.
- **IA: Render Modes en Blazor**: **SIEMPRE** agregar `@rendermode InteractiveServer` en páginas nuevas que contengan eventos `@onclick`, formularios interactivos, o diálogos de MudBlazor. El modo predeterminado (Static SSR) **NO soporta eventos** y causará que los botones no funcionen.

## 🤖 Instrucciones para la IA

Al desarrollar este proyecto:

1. **Respetar el stack tecnológico** definido en la sección principal
2. **Implementar todas las características** listadas en "Arquitectura y Características"
3. **Seguir las convenciones** específicas de cada tecnología y framework
4. **Aplicar buenas prácticas** de seguridad, rendimiento y mantenibilidad
5. **Documentar el código** de forma clara y concisa
6. **Escribir tests** para la funcionalidad crítica según el framework de testing elegido
7. **Aplicar render modes correctamente en Blazor**:
   - **Páginas con eventos (`@onclick`, `@onchange`, etc.)**: Agregar `@rendermode InteractiveServer`
   - **Páginas estáticas (solo lectura)**: No requieren directiva (usan Static SSR)
   - **Nunca aplicar rendermode en `MainLayout.razor`** (causa errores de serialización de RenderFragment)

## 💻 Comandos Útiles

```bash
# Crear proyecto Blazor
dotnet new blazor -n MyBlazorApp

# Restaurar y ejecutar
dotnet restore
dotnet run

# Build para producción
dotnet publish -c Release
```