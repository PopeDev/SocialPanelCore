# Sprint 1: Fundamentos - Modelos y Migraciones

**Duración estimada:** 2-3 días
**Prerrequisitos:** Acceso al repositorio, .NET 10 SDK instalado, PostgreSQL corriendo

---

## Objetivo del Sprint

Crear la base de datos y modelos necesarios para soportar:
- Almacenamiento de medios (imágenes)
- Configuración de AI Optimization por publicación y por red
- Modo de publicación (programada vs inmediata)
- Configuración de medios permitidos por red social

---

## Tareas

### Tarea 1.1: Crear Enum PublishMode

**Archivo a crear:** `SocialPanelCore.Domain/Enums/PublishMode.cs`

```csharp
namespace SocialPanelCore.Domain.Enums;

/// <summary>
/// Modo de publicación de un post
/// </summary>
public enum PublishMode
{
    /// <summary>
    /// Publicación programada para una fecha futura.
    /// Procesada por Hangfire en background.
    /// </summary>
    Scheduled = 0,

    /// <summary>
    /// Publicación inmediata.
    /// Se procesa de forma síncrona cuando el usuario confirma.
    /// </summary>
    Immediate = 1
}
```

**Pasos:**
1. Navega a `SocialPanelCore.Domain/Enums/`
2. Crea el archivo `PublishMode.cs`
3. Copia el código anterior
4. Guarda el archivo

---

### Tarea 1.2: Crear Entidad PostMedia

**Archivo a crear:** `SocialPanelCore.Domain/Entities/PostMedia.cs`

```csharp
namespace SocialPanelCore.Domain.Entities;

/// <summary>
/// Representa un archivo multimedia (imagen) asociado a una publicación.
/// Los archivos se almacenan en el servidor local siguiendo la estructura:
/// /uploads/{NombreCuenta}/{RedSocial}/{Titulo}_{Fecha}_{Guid}.{extension}
/// </summary>
public class PostMedia
{
    /// <summary>
    /// Identificador único del medio
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// ID de la publicación base a la que pertenece este medio
    /// </summary>
    public Guid BasePostId { get; set; }

    /// <summary>
    /// Nombre original del archivo subido por el usuario
    /// Ejemplo: "foto_navidad.jpg"
    /// </summary>
    public string OriginalFileName { get; set; } = string.Empty;

    /// <summary>
    /// Nombre del archivo almacenado en el servidor (incluye GUID para evitar colisiones)
    /// Ejemplo: "publicacion_navidad_24122025_a1b2c3d4.jpg"
    /// </summary>
    public string StoredFileName { get; set; } = string.Empty;

    /// <summary>
    /// Ruta relativa del archivo desde la carpeta uploads
    /// Ejemplo: "peluqueriaspaco/instagram/publicacion_navidad_24122025_a1b2c3d4.jpg"
    /// </summary>
    public string RelativePath { get; set; } = string.Empty;

    /// <summary>
    /// Tipo MIME del archivo
    /// Valores permitidos: "image/jpeg", "image/png"
    /// </summary>
    public string ContentType { get; set; } = string.Empty;

    /// <summary>
    /// Tamaño del archivo en bytes
    /// </summary>
    public long FileSize { get; set; }

    /// <summary>
    /// Orden de la imagen en la publicación (para carruseles)
    /// Comienza en 0
    /// </summary>
    public int SortOrder { get; set; }

    /// <summary>
    /// Fecha de creación del registro
    /// </summary>
    public DateTime CreatedAt { get; set; }

    // ========== Navegación ==========

    /// <summary>
    /// Publicación base a la que pertenece este medio
    /// </summary>
    public virtual BasePost BasePost { get; set; } = null!;
}
```

**Pasos:**
1. Navega a `SocialPanelCore.Domain/Entities/`
2. Crea el archivo `PostMedia.cs`
3. Copia el código anterior
4. Guarda el archivo

---

### Tarea 1.3: Modificar BasePost

**Archivo a modificar:** `SocialPanelCore.Domain/Entities/BasePost.cs`

**Cambios a realizar:**

1. Añadir el using necesario al inicio del archivo:
```csharp
using SocialPanelCore.Domain.Enums;
```

2. Añadir los nuevos campos DESPUÉS de `ContentType` y ANTES de `RequiresApproval`:

```csharp
    public ContentType ContentType { get; set; }

    // ========== NUEVOS CAMPOS - AI OPTIMIZATION ==========

    /// <summary>
    /// Indica si la publicación debe ser optimizada por IA.
    /// Si es true, el contenido se adaptará automáticamente para cada red.
    /// Si es false, el contenido se publicará tal cual (en bruto).
    /// Este valor actúa como "master" que puede ser sobreescrito por cada red individual.
    /// </summary>
    public bool AiOptimizationEnabled { get; set; } = true;

    /// <summary>
    /// Modo de publicación: Scheduled (programada) o Immediate (inmediata)
    /// </summary>
    public PublishMode PublishMode { get; set; } = PublishMode.Scheduled;

    // ========== FIN NUEVOS CAMPOS ==========

    public bool RequiresApproval { get; set; }
```

3. Añadir la navegación a Media DESPUÉS de `AdaptedVersions`:

```csharp
    public virtual ICollection<AdaptedPost> AdaptedVersions { get; set; } = new List<AdaptedPost>();

    /// <summary>
    /// Archivos multimedia (imágenes) asociados a esta publicación
    /// </summary>
    public virtual ICollection<PostMedia> Media { get; set; } = new List<PostMedia>();
}
```

**Archivo completo después de los cambios:**

```csharp
using SocialPanelCore.Domain.Enums;

namespace SocialPanelCore.Domain.Entities;

public class BasePost
{
    public Guid Id { get; set; }
    public Guid AccountId { get; set; }
    public Guid? CreatedByUserId { get; set; }
    public string? Title { get; set; }
    public string Content { get; set; } = string.Empty;
    public DateTime ScheduledAtUtc { get; set; }
    public BasePostState State { get; set; }
    public ContentType ContentType { get; set; }

    // ========== AI OPTIMIZATION ==========

    /// <summary>
    /// Indica si la publicación debe ser optimizada por IA.
    /// Si es true, el contenido se adaptará automáticamente para cada red.
    /// Si es false, el contenido se publicará tal cual (en bruto).
    /// </summary>
    public bool AiOptimizationEnabled { get; set; } = true;

    /// <summary>
    /// Modo de publicación: Scheduled (programada) o Immediate (inmediata)
    /// </summary>
    public PublishMode PublishMode { get; set; } = PublishMode.Scheduled;

    // ========== FIN AI OPTIMIZATION ==========

    public bool RequiresApproval { get; set; }

    // Aprobación/Rechazo
    public Guid? ApprovedByUserId { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public string? ApprovalNotes { get; set; }
    public Guid? RejectedByUserId { get; set; }
    public DateTime? RejectedAt { get; set; }
    public string? RejectionNotes { get; set; }

    // Publicación
    public DateTime? PublishedAt { get; set; }

    // Auditoría
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Navegación
    public virtual Account Account { get; set; } = null!;
    public virtual User? CreatedByUser { get; set; }
    public virtual ICollection<PostTargetNetwork> TargetNetworks { get; set; } = new List<PostTargetNetwork>();
    public virtual ICollection<AdaptedPost> AdaptedVersions { get; set; } = new List<AdaptedPost>();
    public virtual ICollection<PostMedia> Media { get; set; } = new List<PostMedia>();
}
```

---

### Tarea 1.4: Modificar PostTargetNetwork

**Archivo a modificar:** `SocialPanelCore.Domain/Entities/PostTargetNetwork.cs`

**Añadir los siguientes campos DESPUÉS de `NetworkType`:**

```csharp
    public NetworkType NetworkType { get; set; }

    // ========== NUEVOS CAMPOS ==========

    /// <summary>
    /// Indica si esta red específica debe usar optimización por IA.
    /// Puede ser diferente al valor global de BasePost.AiOptimizationEnabled.
    /// Por defecto hereda el valor del BasePost.
    /// </summary>
    public bool UseAiOptimization { get; set; } = true;

    /// <summary>
    /// Indica si esta red debe incluir los medios (imágenes) de la publicación.
    /// Solo tiene efecto si BasePost tiene medios asociados.
    /// Debe respetar SocialChannelConfig.AllowMedia de la red.
    /// </summary>
    public bool IncludeMedia { get; set; } = true;

    // ========== FIN NUEVOS CAMPOS ==========

    // Navegación
```

**Archivo completo después de los cambios:**

```csharp
using SocialPanelCore.Domain.Enums;

namespace SocialPanelCore.Domain.Entities;

/// <summary>
/// Representa una red social objetivo para una publicación.
/// Cada BasePost puede tener múltiples redes objetivo.
/// </summary>
public class PostTargetNetwork
{
    public Guid Id { get; set; }
    public Guid BasePostId { get; set; }
    public NetworkType NetworkType { get; set; }

    // ========== CONFIGURACIÓN POR RED ==========

    /// <summary>
    /// Indica si esta red específica debe usar optimización por IA.
    /// Puede ser diferente al valor global de BasePost.AiOptimizationEnabled.
    /// </summary>
    public bool UseAiOptimization { get; set; } = true;

    /// <summary>
    /// Indica si esta red debe incluir los medios (imágenes) de la publicación.
    /// Solo tiene efecto si BasePost tiene medios asociados.
    /// </summary>
    public bool IncludeMedia { get; set; } = true;

    // ========== FIN CONFIGURACIÓN ==========

    // Navegación
    public virtual BasePost BasePost { get; set; } = null!;
}
```

---

### Tarea 1.5: Modificar SocialChannelConfig

**Archivo a modificar:** `SocialPanelCore.Domain/Entities/SocialChannelConfig.cs`

**Añadir el siguiente campo DESPUÉS de `IsEnabled`:**

```csharp
    public bool IsEnabled { get; set; }

    /// <summary>
    /// Indica si esta red social permite publicar con medios (imágenes/videos).
    /// Ejemplo: X/Twitter puede tener coste adicional por medios, entonces AllowMedia = false.
    /// Instagram normalmente requiere medios, entonces AllowMedia = true.
    /// </summary>
    public bool AllowMedia { get; set; } = true;

    public HealthStatus HealthStatus { get; set; }
```

---

### Tarea 1.6: Modificar ApplicationDbContext

**Archivo a modificar:** `SocialPanelCore.Infrastructure/Data/ApplicationDbContext.cs`

**Paso 1:** Añadir el DbSet para PostMedia (después de los otros DbSets):

```csharp
public DbSet<AdaptedPost> AdaptedPosts { get; set; }
public DbSet<PostMedia> PostMedia { get; set; }  // NUEVO
public DbSet<UserAccountAccess> UserAccountAccess { get; set; }
```

**Paso 2:** Añadir la configuración de PostMedia en `OnModelCreating`:

Busca el método `OnModelCreating` y añade la siguiente configuración ANTES del cierre del método:

```csharp
    // ========== CONFIGURACIÓN DE PostMedia ==========
    modelBuilder.Entity<PostMedia>(entity =>
    {
        entity.HasKey(pm => pm.Id);

        entity.Property(pm => pm.OriginalFileName)
            .IsRequired()
            .HasMaxLength(255);

        entity.Property(pm => pm.StoredFileName)
            .IsRequired()
            .HasMaxLength(500);

        entity.Property(pm => pm.RelativePath)
            .IsRequired()
            .HasMaxLength(1000);

        entity.Property(pm => pm.ContentType)
            .IsRequired()
            .HasMaxLength(100);

        // Relación con BasePost
        entity.HasOne(pm => pm.BasePost)
            .WithMany(bp => bp.Media)
            .HasForeignKey(pm => pm.BasePostId)
            .OnDelete(DeleteBehavior.Cascade);

        // Índice para búsqueda por post
        entity.HasIndex(pm => pm.BasePostId);
    });
```

---

### Tarea 1.7: Generar Migración

**IMPORTANTE:** Ejecuta estos comandos desde la carpeta raíz del proyecto (`/home/user/SocialPanelCore`)

**Paso 1:** Verificar que el proyecto compila sin errores:
```bash
dotnet build
```

Si hay errores, revisa los pasos anteriores. Los errores comunes son:
- Falta un `using` statement
- Error de sintaxis (falta punto y coma, llave, etc.)
- Nombre de propiedad mal escrito

**Paso 2:** Generar la migración:
```bash
dotnet ef migrations add AddMediaAndAiOptimization --project SocialPanelCore.Infrastructure --startup-project .
```

**Explicación del comando:**
- `dotnet ef migrations add` - Crea una nueva migración
- `AddMediaAndAiOptimization` - Nombre descriptivo de la migración
- `--project SocialPanelCore.Infrastructure` - Proyecto donde está el DbContext
- `--startup-project .` - Proyecto de inicio (el actual)

**Paso 3:** Verificar la migración generada

Abre el archivo generado en `Migrations/` y verifica que contiene:
- Creación de tabla `PostMedia`
- Columnas nuevas en `BasePosts`: `AiOptimizationEnabled`, `PublishMode`
- Columnas nuevas en `PostTargetNetworks`: `UseAiOptimization`, `IncludeMedia`
- Columna nueva en `SocialChannelConfigs`: `AllowMedia`

**Paso 4:** Aplicar la migración:
```bash
dotnet ef database update --project SocialPanelCore.Infrastructure --startup-project .
```

---

### Tarea 1.8: Verificar la Base de Datos

Conéctate a PostgreSQL y verifica las tablas:

```sql
-- Verificar nuevas columnas en BasePosts
SELECT column_name, data_type
FROM information_schema.columns
WHERE table_name = 'BasePosts'
AND column_name IN ('AiOptimizationEnabled', 'PublishMode');

-- Verificar nuevas columnas en PostTargetNetworks
SELECT column_name, data_type
FROM information_schema.columns
WHERE table_name = 'PostTargetNetworks'
AND column_name IN ('UseAiOptimization', 'IncludeMedia');

-- Verificar nueva columna en SocialChannelConfigs
SELECT column_name, data_type
FROM information_schema.columns
WHERE table_name = 'SocialChannelConfigs'
AND column_name = 'AllowMedia';

-- Verificar tabla PostMedia
SELECT column_name, data_type
FROM information_schema.columns
WHERE table_name = 'PostMedia';
```

---

## Criterios de Aceptación

- [ ] El proyecto compila sin errores
- [ ] La migración se genera correctamente
- [ ] La migración se aplica sin errores
- [ ] La tabla `PostMedia` existe con todas las columnas
- [ ] `BasePosts` tiene las columnas `AiOptimizationEnabled` y `PublishMode`
- [ ] `PostTargetNetworks` tiene las columnas `UseAiOptimization` e `IncludeMedia`
- [ ] `SocialChannelConfigs` tiene la columna `AllowMedia`

---

## Problemas Comunes y Soluciones

### Error: "The entity type 'PostMedia' was not found"

**Causa:** Falta el DbSet en ApplicationDbContext

**Solución:** Asegúrate de añadir `public DbSet<PostMedia> PostMedia { get; set; }`

### Error: "Unable to create an object of type 'ApplicationDbContext'"

**Causa:** Falta la cadena de conexión o el proyecto de inicio está mal configurado

**Solución:**
```bash
dotnet ef migrations add ... --startup-project . --verbose
```

### Error: "Column 'xxx' already exists"

**Causa:** La migración ya se aplicó parcialmente

**Solución:**
1. Elimina la migración: `dotnet ef migrations remove`
2. Verifica el estado de la BD
3. Regenera la migración

---

## Siguiente Sprint

Una vez completado este sprint, continúa con:
- **Sprint 2:** `docs/sprint2-view-edit.md` - Páginas View y Edit
