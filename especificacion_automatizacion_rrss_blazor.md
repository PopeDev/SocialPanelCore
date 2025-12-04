
# ESPECIFICACIÓN FUNCIONAL Y TÉCNICA  
## Plataforma de planificación y automatización de publicaciones en redes sociales  
### Versión: 1.0  
### Idioma de trabajo: es-ES

---

## 0. Objetivo del documento

Este documento define **de forma ultra detallada**:

1. La **lógica de negocio / dominio** de la aplicación.
2. Los **requisitos técnicos** y una propuesta de estructura técnica concreta en .NET + Blazor.

El objetivo es que una IA de generación de código (p. ej. OpenAI Codex / GPT para código) pueda implementar la solución **sin ambigüedades**.  
Si hay errores en la implementación, deben deberse a la IA de código, **no** a falta de precisión en este documento.

---

## 1. Visión general del producto

La aplicación es una herramienta interna tipo “mini-Hootsuite” para:

- Gestionar **negocios** (llamados en el dominio: **Cuentas**).
- Conectar las redes sociales de cada Cuenta (Instagram, Facebook, TikTok, X, YouTube, LinkedIn, etc.).
- Planificar publicaciones mediante un **calendario** por Cuenta.
- Crear una **Publicación base** (contenido madre) por fecha/hora y Cuenta.
- Utilizar un **LLM** (modelo de lenguaje) para adaptar automáticamente esa Publicación base a cada red social activa de la Cuenta (variantes por red).
- Publicar automáticamente las variantes en cada red a la hora programada.

La vista principal para el usuario dentro de una Cuenta es el **calendario de publicaciones**.

---

## 2. Alcance funcional (V1)

Incluido en esta versión:

- Gestión de usuarios con dos roles: **Superadministrador** y **Usuario básico**.
- Gestión de **Cuentas** (negocios).
- Configuración de redes sociales para cada Cuenta mediante flujo de conexión OAuth (ej.: “Conectar Instagram”).  
- Planificación de **Publicaciones base** en un calendario por Cuenta.
- Generación automática de **Variantes de publicación por red social** usando IA.
- Publicación automática de las variantes en las redes sociales.
- Manejo básico de errores: redes KO, tokens caducados, errores de publicación.
- Idioma único de trabajo: **es-ES**.

No incluido (se podrá añadir en versiones posteriores):

- Analítica avanzada (métricas de rendimiento de publicaciones).
- Moderación de comentarios.
- Roles más granulares (permisos por tipo de acción dentro de una Cuenta).
- Soporte multi-idioma distinto a es-ES.

---

## 3. Glosario de términos de dominio

- **Usuario**: persona que accede a la aplicación con credenciales propias.
- **Superadministrador**: usuario con acceso global a todas las Cuentas y con permisos de gestión de usuarios y Cuentas.
- **Usuario básico**: usuario con acceso solo a las Cuentas que se le han asignado.
- **Cuenta**: representa un negocio / cliente / marca (ej.: “Inmobiliaria Jaén”, “Peluquería Divas Hair”). Cada Cuenta agrupa sus redes sociales y sus publicaciones.
- **Red social soportada**: uno de los tipos fijos de redes que el sistema conoce globalmente (Facebook, Instagram, TikTok, X, YouTube, LinkedIn, etc.).
- **Configuración de red social de una Cuenta**: configuración concreta de, por ejemplo, “Instagram de Inmobiliaria Jaén”: tokens, estado activado/desactivado, estado de salud (OK/KO), etc.
- **Publicación base**: contenido madre creado por el usuario para una Cuenta y una fecha/hora concreta. Define qué se quiere comunicar, con qué medios y a qué redes de esa Cuenta se pretende enviar.
- **Variante de publicación**: adaptación específica de una Publicación base para una red social concreta (ej.: “versión TikTok”, “versión Instagram”) generada por IA.
- **LLM**: modelo de lenguaje (IA) utilizado para adaptar el contenido base a cada red social.
- **Calendario de publicaciones**: vista principal donde el usuario ve, crea y gestiona las Publicaciones base de una Cuenta.

---

## 4. Lógica de negocio / dominio

### 4.1. Roles de usuario

#### 4.1.1. Superadministrador

**Capacidades:**

- Puede autenticarse en la aplicación.
- Puede ver y operar sobre **todas** las Cuentas del sistema.
- Puede hacer **todo lo que hace un Usuario básico** en cualquier Cuenta.
- Además, puede:
  - Crear, editar y desactivar Usuarios.
  - Asignar Cuentas a Usuarios básicos.
  - Crear, editar y eliminar Cuentas.

#### 4.1.2. Usuario básico

**Capacidades:**

- Puede autenticarse en la aplicación.
- Solo puede ver y operar sobre las **Cuentas que tenga asignadas**.
- En las Cuentas asignadas puede:
  - Ver la configuración de la Cuenta.
  - Gestionar redes sociales de la Cuenta (conectar/desconectar redes, según lo que se decida permitir).
  - Ver y utilizar el calendario de publicaciones.
  - Crear, editar, duplicar y cancelar Publicaciones base.
  - Consultar el estado de las publicaciones y sus variantes.

**Restricciones:**

- No puede crear ni eliminar Cuentas.
- No puede gestionar otros Usuarios ni modificar asignaciones de Cuentas de otros usuarios.

---

### 4.2. Entidad de dominio: Usuario

**Descripción:** representa a una persona que utiliza el sistema.

**Atributos de negocio relevantes:**

- Identificador único.
- Nombre.
- Email (único, usado para login).
- Rol (Superadministrador | UsuarioBásico).
- Relación con Cuentas (para Usuario básico): lista de Cuentas a las que tiene acceso.

**Reglas de negocio:**

- Un Usuario básico debe tener asignada al menos una Cuenta para que la aplicación tenga sentido para él.
- Un Superadministrador tiene acceso implícito a todas las Cuentas, sin necesidad de asignación explícita.
- El email debe ser único en el sistema.

---

### 4.3. Entidad de dominio: Cuenta (negocio)

**Descripción:** representa un negocio/cliente/marca sobre el que se gestionan redes y publicaciones.

**Atributos de negocio:**

- Identificador único.
- Nombre de la Cuenta (ej.: “Inmobiliaria Jaén”).
- Descripción (opcional).
- Colección de configuraciones de redes sociales asociados a la Cuenta (ver 4.4).
- Colección de Usuarios básicos asignados a la Cuenta.
- Preferencias opcionales (futuro): tono de comunicación, reglas de contenido, etc.  
  > Nota: en V1 el idioma/locale se asume siempre `es-ES` y no se modela por Cuenta.

**Reglas de negocio:**

- Una Cuenta siempre tiene un nombre no vacío.
- Una Cuenta puede existir sin redes conectadas (p. ej. todavía en fase de alta).
- Una Cuenta puede existir sin Usuarios básicos asignados (visible solo para Superadmins).

---

### 4.4. Entidad de dominio: Configuración de red social de una Cuenta

Esta entidad representa la configuración de una **red concreta** dentro de una Cuenta, por ejemplo: “Instagram de Inmobiliaria Jaén”.

**Las redes soportadas globalmente** son, al menos:

- Facebook
- Instagram
- TikTok
- X (Twitter)
- YouTube
- LinkedIn

Se asume que esta lista es estática a nivel de sistema (pero ampliable en futuras versiones).

#### 4.4.1. Atributos de negocio

Para cada combinación (Cuenta + RedSocial) existirá una configuración con los siguientes atributos:

- `NetworkType` (tipo de red social):  
  Valor de una enumeración fija: { Facebook, Instagram, TikTok, X, YouTube, LinkedIn, ... }.

- `IsEnabled` (booleano):  
  - `true`: la red está activada para esta Cuenta (el negocio **quiere** usarla).  
  - `false`: la red está desactivada para esta Cuenta (no se usa).

- `HealthStatus` (estado de salud de la conexión):  
  - `OK` → se considera que la conexión está operativa (token válido, última llamada correcta).  
  - `KO` → la red está configurada pero hay algún problema (token caducado, error, configuración incompleta, etc.).

- Datos de conexión mínimos:
  - `AccessToken`: token de acceso actual emitido por la red social.
  - `RefreshToken` (opcional, solo si aplica para esa red).
  - `TokenExpiresAt` (opcional, solo si la red lo provee).
  - `AuthMethod`: valor simple indicando el método de autenticación (por ejemplo: `OAuth`, `ApiKey`).

- Identificación básica:
  - `ExternalId`: identificador de la cuenta/página/canal en la red social (ej.: pageId, userId, channelId).
  - `Handle` o nombre público (ej.: `@inmojaen`).

> **Nota importante:**  
> No se almacenan usuarios ni contraseñas de redes sociales.  
> Solo se guardan tokens emitidos por los proveedores (ej. vía OAuth).

#### 4.4.2. Reglas de negocio relacionadas

- Si `IsEnabled = false`, la red:
  - No debe aparecer como opción al crear una Publicación base.
  - No se generan variantes ni se publica nada en esa red.

- Si `IsEnabled = true` y `HealthStatus = OK`:
  - La red está **operativa** para planificación, generación de variantes y publicación.

- Si `IsEnabled = true` y `HealthStatus = KO`:
  - La red se considera configurada pero con problemas.
  - Lo recomendable en la implementación:
    - O bien no se permite seleccionarla al planificar.
    - O bien se permite, pero la publicación para esa red fallará y la variante quedará en estado Error, dejando claro el problema al usuario.

- El sistema **no utiliza** redes con `HealthStatus = KO` para publicación automática (esto debe quedar claro en la lógica).

---

### 4.5. Entidad de dominio: Publicación base

**Descripción:**  
Representa una unidad de contenido planificada para una Cuenta en una fecha/hora determinada. Es el “contenido madre” del que derivan las variantes por red social.

**Atributos de negocio:**

- Identificador único.
- Cuenta a la que pertenece (referencia).
- Usuario que la ha creado (referencia, opcional para auditoría).
- Fecha y hora **programadas** de publicación (almacenadas en UTC en la base de datos).
- Título (opcional, para identificar rápidamente la publicación en el calendario).
- Contenido base (texto principal).
- Medios asociados (puede ser una colección de referencias a ficheros: imágenes, vídeos, etc.).
- Lista de redes sociales objetivo para esta publicación: conjunto de tipos de red (ej.: { Instagram, TikTok, X }).  
  > Importante: solo deben poder seleccionarse redes que para esa Cuenta tengan `IsEnabled = true`.

- Estado de la Publicación base. Estados posibles:
  - `Borrador`: aún en edición, no debe entrar en automatización.
  - `Planificada`: lista para entrar en el flujo de adaptación IA y publicación.
  - `AdaptaciónPendiente`: estado interno opcional, indica que está pendiente de que el proceso de IA genere las variantes.
  - `Adaptada`: todas las variantes han sido generadas (aunque no publicadas).
  - `ParcialmentePublicada`: algunas variantes se han publicado con éxito y otras han fallado o están pendientes.
  - `Publicada`: todas las variantes seleccionadas se han publicado correctamente.
  - `Cancelada`: el usuario cancela la publicación (no debe publicarse nada).

**Reglas de negocio:**

- Una Publicación base pertenece exactamente a una Cuenta.
- Una Publicación base puede apuntar a una o varias redes objetivo.
- Una Publicación base en estado `Borrador` o `Cancelada` **no** debe ser considerada por los procesos automáticos de adaptación IA ni publicación.
- La fecha/hora programada de una Publicación base debe ser una fecha válida (puede estar en pasado, futuro o presente; la lógica deberá decidir qué hacer si está en pasado).

---

### 4.6. Entidad de dominio: Variante de publicación por red social

**Descripción:**  
Es la versión adaptada de una Publicación base para una red social concreta de la Cuenta, generada por un LLM.

**Atributos de negocio:**

- Identificador único.
- Referencia a la Publicación base.
- Referencia a la Configuración de red social (Cuenta + NetworkType).
- Campo `NetworkType` redundante (para facilitar consultas simples).
- Contenido adaptado (texto final).
- Hashtags (texto o colección).
- Medios adaptados (en V1 puede ser la misma referencia que la Publicación base; en el futuro se puede adaptar formato).
- Fecha/hora de publicación programada (normalmente igual a la de la Publicación base, pero se puede permitir un pequeño ajuste).
- Estado de la Variante. Estados posibles mínimamente:
  - `PendienteDeGeneración`: definida por la lógica, aún no se ha llamado a la IA.
  - `Generada`: la IA ha generado el contenido adaptado.
  - `ListaParaPublicar`: ha sido generada y está lista para entrar en la cola de publicación.
  - `Publicada`: la publicación en la red se ha completado con éxito.
  - `ErrorGeneración`: la IA no ha podido generar el contenido correctamente.
  - `ErrorPublicación`: la red social ha devuelto un error al intentar publicar.
  - `Cancelada`: la variante se ha cancelado explícitamente (por ejemplo, si la Publicación base se cancela).

- Información de publicación:
  - Fecha/hora real de publicación (si se ha publicado).
  - Identificador de publicación en la red social (si aplica).
  - Mensaje de error de la última operación fallida (si aplica).

**Reglas de negocio:**

- Para cada Publicación base y cada red social seleccionada con `IsEnabled = true` y `HealthStatus = OK`, debe existir como máximo una Variante “activa”.
- El estado de la Publicación base depende de los estados de sus Variantes:
  - Todas las variantes Publicadas → Publicación base = `Publicada`.
  - Al menos una variante Publicada y otras con Error o pendientes → Publicación base = `ParcialmentePublicada`.
- Solo las Variantes en estado `ListaParaPublicar` deben ser consideradas por el proceso automático de publicación.

---

### 4.7. Reglas globales de negocio (resumen)

1. **Idioma / locale:**  
   - Todo el contenido generado por IA y mostrado al usuario se asume en idioma `es-ES`.
   - No hay configuración de idioma por Cuenta ni por red en V1.

2. **Permisos por Cuenta:**  
   - Si un Usuario tiene acceso a una Cuenta, puede utilizar todas las redes activas de esa Cuenta con todas sus capacidades (publicar posts, stories, etc.).  
   - No se definen permisos finos por tipo de acción o tipo de contenido.

3. **Redes activas:**  
   - Una red debe tener `IsEnabled = true` y `HealthStatus = OK` para ser considerada en planificación, adaptación y publicación.
   - Las redes con `IsEnabled = false` o `HealthStatus = KO` no deben participar en procesos automáticos.

4. **Restricciones técnicas por red:**  
   - Longitud máxima de texto, tipos de media soportados, uso de hashtags, etc.  
   - En V1 se gestionan mediante lógica hardcodeada en un servicio, **no** como campos configurables en el dominio.

---

## 5. Flujos de negocio detallados

### 5.1. Flujo: login de usuario

**Objetivo:** permitir que un usuario acceda a la aplicación.

**Pasos de negocio:**

1. El usuario introduce email y contraseña.
2. El sistema valida las credenciales.
3. Si son correctas:
   - Se inicia sesión y se determina el rol (Superadmin / Usuario básico).
4. Si son incorrectas:
   - Se informa al usuario de error de login.

**Resultado en la aplicación:**

- Si el usuario es Superadmin:
  - Puede acceder a la vista de gestión de Usuarios y Cuentas, además de sus Cuentas.

- Si el usuario es básico:
  - Accede a la lista de Cuentas a las que tiene asignadas.

---

### 5.2. Flujo: gestión de Usuarios (solo Superadmin)

**Caso 1: Crear un Usuario básico**

1. Superadmin abre la sección “Usuarios”.
2. Crea un nuevo usuario con:
   - Nombre.
   - Email.
   - Rol = Usuario básico.
3. Asigna una o varias Cuentas al nuevo usuario.
4. El usuario básico podrá autenticarse y ver únicamente esas Cuentas.

**Caso 2: Asignar o quitar Cuentas a un Usuario básico existente**

1. Superadmin selecciona un Usuario básico.
2. Añade o elimina Cuentas de su lista de asignadas.
3. Los cambios afectan inmediatamente a las Cuentas que el usuario ve.

---

### 5.3. Flujo: gestión de Cuentas (negocios)

**Caso 1: Crear una Cuenta (solo Superadmin)**

1. Superadmin accede a “Cuentas”.
2. Crea una nueva Cuenta indicando:
   - Nombre.
   - Descripción opcional.
3. La Cuenta se crea sin redes configuradas y sin Usuarios básicos asignados (de inicio).

**Caso 2: Asignar Usuarios básicos a una Cuenta**

1. Superadmin entra en el detalle de una Cuenta.
2. En la sección de “Usuarios asignados”:
   - Añade uno o varios Usuarios básicos.
3. Esos Usuarios básicos verán esta Cuenta en su lista al iniciar sesión.

---

### 5.4. Flujo crítico: Conectar una red social a una Cuenta (OAuth)

Este flujo aplica a cualquier red social que use OAuth (Instagram, Facebook, TikTok, YouTube, LinkedIn, etc.).

**Objetivo:** obtener y almacenar un `AccessToken` (y opcionalmente `RefreshToken`) para una red social concreta de una Cuenta, sin almacenar usuario/contraseña.

**Pasos de negocio:**

1. El usuario (Superadmin o Usuario básico con permisos) entra en la configuración de una Cuenta.
2. En la sección de redes sociales, ve la lista de redes soportadas.
3. En una red desactivada o no configurada, pulsa el botón **“Conectar [Red]”**.
4. El sistema redirige al usuario a la página de autorización de la red social (OAuth):
   - El usuario introduce sus credenciales **en la web de la red social**, no en la app.
   - La red social muestra una pantalla de consentimiento indicando los permisos (publicar, etc.).
5. El usuario acepta la autorización.
6. La red social redirige de vuelta a la aplicación con un código de autorización.
7. La aplicación intercambia ese código por un `AccessToken` (y opcionalmente `RefreshToken` y datos de expiración).
8. La aplicación almacena esos datos en la Configuración de red social de la Cuenta:
   - `AccessToken`
   - `RefreshToken` (si lo hay)
   - `TokenExpiresAt` (si lo hay)
   - `AuthMethod = OAuth`
   - `IsEnabled = true`
   - `HealthStatus = OK`
   - `ExternalId` y `Handle` (obtenidos en esta fase si es posible).
9. La red pasa a estar disponible para planificación, adaptación y publicación.

**Casos de error:**

- El usuario cancela en la pantalla de la red social:
  - La aplicación no guarda tokens.
  - La red sigue `IsEnabled = false`.
  - `HealthStatus` no cambia o se mantiene como no configurada.
- El intercambio de código por token falla:
  - No se guardan tokens.
  - Se informa al usuario y se mantiene la red como no operativa.

**Regla principal:**  
> La aplicación **no guarda contraseñas** de redes sociales; únicamente tokens emitidos por los proveedores.

---

### 5.5. Flujo: Configurar activación/desactivación de redes

**Objetivo:** decidir qué redes se usan en una Cuenta.

**Pasos:**

1. Usuario entra en la configuración de una Cuenta.
2. Ve la lista de redes soportadas.
3. Para cada red:
   - Puede activar/desactivar la red (si tiene permisos).
   - Al activar:
     - Si no estaba conectada, puede lanzar el flujo de conexión (OAuth).
   - Al desactivar:
     - `IsEnabled = false` → la red deja de estar disponible para nuevas Publicaciones base.

**Efectos:**

- Las Publicaciones base futuras solo podrán seleccionar redes con `IsEnabled = true`.
- Las Publicaciones base existentes que apuntaban a redes ahora desactivadas requerirán una lógica clara (por ejemplo: impedir su publicación en esa red, marcar variantes como canceladas, etc.).

---

### 5.6. Flujo: Crear una Publicación base desde el calendario

**Actor:** Usuario básico o Superadmin.

**Objetivo:** planificar una nueva Publicación base.

**Pasos de negocio:**

1. El usuario selecciona una Cuenta a la que tiene acceso.
2. La vista principal muestra el **calendario de publicaciones** de esa Cuenta.
3. El usuario hace clic en una fecha/hora del calendario (o pulsa un botón “Nueva publicación” y luego elige fecha/hora).
4. Rellena un formulario con:
   - Título (opcional).
   - Contenido base (texto).
   - Medios (subida/selección de archivos, opcional).
   - Fecha y hora programadas.
   - Selección de redes sociales objetivo (solo redes de esa Cuenta con `IsEnabled = true` y, preferiblemente, `HealthStatus = OK`).
5. El usuario decide el estado inicial:
   - `Borrador`: si aún no quiere que entre en automatización.
   - `Planificada`: si quiere que el sistema ya la tenga en cuenta para adaptación y publicación.
6. Guarda la Publicación base.

**Resultado:**

- La Publicación base aparece en el calendario en la fecha/hora seleccionadas, con su estado correspondiente.

---

### 5.7. Flujo: Modificar / reprogramar una Publicación base

**Pasos:**

1. El usuario abre una Publicación base desde el calendario.
2. Puede modificar:
   - Contenido base.
   - Medios.
   - Fecha/hora programadas.
   - Redes objetivo.
3. Si la Publicación base ya tenía Variantes generadas, la lógica debe decidir:
   - O bien invalidar las Variantes anteriores y marcarlas como obsoletas (y regenerar).
   - O bien mantenerlas pero actualizar la fecha/hora si solo se ha cambiado eso.

**Regla recomendada:**

- Si se modifica el contenido base o las redes seleccionadas **después** de que se hayan generado Variantes:
  - Se deben marcar las Variantes existentes como obsoletas o eliminarlas.
  - La Publicación base vuelve a estado `Planificada`/`AdaptaciónPendiente`.
  - El siguiente proceso de IA generará Variantes nuevas con la nueva información.

---

### 5.8. Flujo: proceso automático de adaptación IA (batch)

**Actor:** proceso en background (no iniciado por usuario).

**Objetivo:** generar automáticamente Variantes por red social a partir de Publicaciones base planificadas.

**Momento de ejecución:**  
- Se recomienda ejecutarlo en intervalos periódicos (p. ej. una vez al día de madrugada, o cada X minutos), pero la lógica de negocio es independiente de la implementación concreta.

**Pasos de negocio:**

1. El proceso busca todas las Publicaciones base que:
   - Estén en estado `Planificada` o `AdaptaciónPendiente`.
   - Tengan una fecha/hora programadas dentro de un rango próximo (ej.: próximas 24 horas).
2. Para cada Publicación base:
   - Para cada red objetivo de la Publicación base:
     - Verifica en la Configuración de la red de esa Cuenta:
       - `IsEnabled = true`
       - `HealthStatus = OK`
     - Si ambas condiciones se cumplen:
       - Si no existe una Variante para esa Publicación base y esa red, o está obsoleta:
         - Se llama al LLM con:
           - Contenido base.
           - Datos de la Cuenta (nombre, sector, etc., si se desea).
           - Tipo de red (para aplicar reglas técnicas y de estilo).
           - Idioma = `es-ES`.
         - El LLM devuelve un contenido adaptado (texto, hashtags, etc.).
         - Se crea/actualiza la Variante con estado `Generada` o directamente `ListaParaPublicar`.
     - Si la red no cumple las condiciones (IsEnabled=false o HealthStatus=KO):
       - No se genera Variante (o se deja constancia de que no se puede).
3. Si todas las Variantes necesarias para una Publicación base se han generado correctamente:
   - La Publicación base pasa a estado `Adaptada` (opcionalmente, si se quiere usar este estado).

**Manejo de errores de IA:**

- Si el LLM devuelve un error o no genera contenido adecuado:
  - La Variante para esa red pasa a estado `ErrorGeneración`.
  - Se registra un mensaje de error.
  - La Publicación base puede pasar a un estado “Adaptada con errores” o mantenerse en `Planificada` con un indicador de error.

---

### 5.9. Flujo: proceso automático de publicación

**Actor:** proceso en background.

**Objetivo:** publicar automáticamente en la red social las Variantes que ya están listas y cuya hora ha llegado.

**Momento de ejecución:**  
- Proceso recurrente (ej. cada minuto) que comprueba Variantes pendientes.

**Pasos de negocio:**

1. El proceso identifica todas las Variantes que:
   - Están en estado `ListaParaPublicar` (o `Generada` si se decide publicar directamente tras generar).
   - Tienen fecha/hora programadas menor o igual a “ahora” (en UTC).
2. Para cada Variante:
   - Comprueba de nuevo que la red correspondiente en la Cuenta:
     - Tiene `IsEnabled = true`.
     - Tiene `HealthStatus = OK`.
   - Si no se cumple, la Variante pasa a `ErrorPublicación` y se registra un error.
   - Si se cumple, el proceso llama a la API de la red social usando el `AccessToken`:
     - Envía el contenido adaptado y los medios.
3. Resultado de la llamada:
   - Si éxito:
     - La Variante pasa a estado `Publicada`.
     - Se guarda el identificador de la publicación en la red social.
   - Si error (token caducado, error de permisos, error de validación, etc.):
     - La Variante pasa a estado `ErrorPublicación`.
     - Se registra el código y mensaje de error.
     - La Configuración de la red puede pasar a `HealthStatus = KO` si el error es de autenticación o permisos.

4. Después de procesar las Variantes, el proceso revisa la Publicación base de cada una:
   - Si todas las Variantes objetivo están en `Publicada`:
     - Publicación base = `Publicada`.
   - Si algunas Variantes están en `Publicada` y otras en error:
     - Publicación base = `ParcialmentePublicada`.

---

### 5.10. Flujo: gestión de errores y reconexión de redes

**Errores de publicación por token caducado o permisos insuficientes:**

1. La API de la red devuelve un error indicando token inválido o caducado.
2. La aplicación marca:
   - La Variante afectada como `ErrorPublicación`.
   - La Configuración de la red como `HealthStatus = KO`.
3. El usuario verá en la configuración de la Cuenta que esa red está en estado KO.
4. El usuario debe lanzar de nuevo el flujo de **“Conectar [Red]”** (OAuth).
5. Si el flujo de conexión se completa con éxito:
   - Se actualizan `AccessToken`, `RefreshToken`, `TokenExpiresAt`.
   - `HealthStatus` pasa a `OK`.
6. El usuario podrá reintentar manualmente la publicación de las Variantes en error (según se implemente).

---

## 6. Requisitos técnicos

### 6.1. Stack tecnológico base

- **Lenguaje:** C#.
- **Framework:** .NET 10.0.
- **Frontend + Backend:** Blazor United/Auto (render mode automático con SSR + InteractiveServer + InteractiveWebAssembly).
- **Persistencia:** Entity Framework Core + PostgreSQL.
- **Autenticación de usuarios:** ASP.NET Core Identity.
- **Procesos en background:** Hosted Services (`IHostedService` / `BackgroundService`) en ASP.NET Core.
- **Proveedor de IA:** OpenRouter con modelo Kimi k2 free.
- **Almacenamiento de medios:** Sistema de archivos local (wwwroot/mediavault).

> Nota: El usuario ha indicado que no se desea una API separada: las páginas Blazor usarán servicios de dominio directamente, en el mismo proyecto/solución.

---

### 6.2. Estructura sugerida de proyectos / namespaces

Aunque todo puede estar en un único proyecto, se recomienda separar lógicamente:

- `Domain`  
  - Entidades de dominio (Usuario, Cuenta, ConfiguraciónRedSocial, PublicaciónBase, VariantePublicación).
  - Enumeraciones: `UserRole`, `NetworkType`, estados de PublicaciónBase y VariantePublicación.
  - Interfaces de servicios de dominio.

- `Infrastructure`  
  - DbContext y configuración de Entity Framework Core.
  - Repositorios (si se usan).
  - Implementaciones de acceso a APIs externas (Instagram, TikTok, etc.).
  - Implementación de almacenamiento seguro de tokens (en BD cifrada, por ejemplo).

- `Application` (puede coincidir con el proyecto Blazor Server)  
  - Servicios de aplicación que orquestan casos de uso (apoyados en el dominio).
  - Hosted Services para adaptación IA y publicación.
  - Integración con proveedores de IA.

- `UI` (Blazor)  
  - Páginas `.razor`.
  - Componentes compartidos (calendario, formularios de publicación, etc.).

> Esto puede ser una separación por namespaces dentro de un solo proyecto para simplificar.

---

### 6.3. Modelo de datos (tablas / entidades persistentes)

Se propone el siguiente esquema de tablas (adaptable al ORM):

#### 6.3.1. Tabla `Users`

- `Id` (GUID o int identity).
- `Name` (nvarchar).
- `Email` (nvarchar, único).
- `PasswordHash` (si se usa Identity, se delega en sus campos).
- `Role` (smallint o nvarchar: `Superadmin` / `Basic`).

#### 6.3.2. Tabla `Accounts` (Cuentas)

- `Id` (GUID/int).
- `Name` (nvarchar).
- `Description` (nvarchar, nullable).
- Timestamps de auditoría (CreatedAt, UpdatedAt).

#### 6.3.3. Tabla `UserAccounts` (relación muchos-a-muchos Usuario básico ↔ Cuenta)

- `UserId`.
- `AccountId`.

> Los Superadmin no necesitan esta tabla para acceso, solo para datos de auditoría si se desea.

#### 6.3.4. Tabla `SocialChannelConfigs` (Configuración de red social por Cuenta)

- `Id`.
- `AccountId` (FK a `Accounts`).
- `NetworkType` (int/ smallint / nvarchar, según la enum de red).
- `IsEnabled` (bit).
- `HealthStatus` (int/ smallint / nvarchar: `OK` / `KO`).

- Datos de conexión mínimos:
  - `AccessToken` (nvarchar, cifrado si es posible).
  - `RefreshToken` (nvarchar, nullable).
  - `TokenExpiresAt` (datetime, nullable).
  - `AuthMethod` (nvarchar: p. ej. `OAuth`).

- Identificación:
  - `ExternalId` (nvarchar).
  - `Handle` (nvarchar).

- Auditoría:
  - `CreatedAt`, `UpdatedAt`.

#### 6.3.5. Tabla `BasePosts` (Publicaciones base)

- `Id`.
- `AccountId` (FK a `Accounts`).
- `CreatedByUserId` (FK a `Users`).
- `Title` (nvarchar, nullable).
- `Content` (nvarchar(max)).
- `ScheduledAtUtc` (datetime).
- `State` (enum persistida: `Draft`, `Planned`, `Adapted`, `PartiallyPublished`, `Published`, `Cancelled`).

- Redes objetivo (dos posibles enfoques):
  1. Tabla de relación `BasePostTargetNetworks` (recomendado):
     - `BasePostId`
     - `NetworkType`
  2. Campo serializado (no recomendado).

- Referencias a medios:
  - Se puede tener una tabla `BasePostMedia` que referencie a ficheros (ruta/URL).

#### 6.3.6. Tabla `PostVariants` (Variantes por red)

- `Id`.
- `BasePostId` (FK a `BasePosts`).
- `SocialChannelConfigId` (FK a `SocialChannelConfigs`).
- `NetworkType` (redundante, para consultas rápidas).
- `Content` (texto adaptado).
- `Hashtags` (nvarchar, nullable).
- `ScheduledAtUtc` (datetime; normalmente igual a la del BasePost).
- `State` (enum persistida: `PendingGeneration`, `Generated`, `ReadyToPublish`, `Published`, `GenerationError`, `PublishError`, `Cancelled`).

- Datos de publicación:
  - `PublishedAtUtc` (datetime, nullable).
  - `ExternalPostId` (nvarchar, nullable).
  - `LastError` (nvarchar, nullable).

- Medios adaptados:
  - Campo(s) que referencian a media concreto (puede ser tabla `PostVariantMedia` si se quiere).

---

### 6.4. Servicios de dominio propuestos

#### 6.4.1. Servicio de gestión de Cuentas

**Responsabilidades:**

- Crear, editar y eliminar Cuentas (solo Superadmin).
- Asignar y desasignar Usuarios básicos a Cuentas.
- Consultar Cuentas visibles para un Usuario.

#### 6.4.2. Servicio de configuración de redes sociales

**Responsabilidades:**

- Listar, activar y desactivar redes para una Cuenta.
- Iniciar y finalizar el flujo de conexión OAuth (sin implementar aquí detalles HTTP, solo a nivel de interfaz).
- Actualizar tokens (`AccessToken`, `RefreshToken`, `TokenExpiresAt`) tras el flujo OAuth.
- Actualizar `IsEnabled` y `HealthStatus`.

#### 6.4.3. Servicio de planificación de publicaciones

**Responsabilidades:**

- Crear Publicaciones base.
- Modificar Publicaciones base.
- Cancelar Publicaciones base.
- Cambiar estado (Draft, Planned, Cancelled).

#### 6.4.4. Servicio de adaptación de contenido (IA)

Interfaz conceptual, por ejemplo:

- Método: `GenerarVariante(BasePost, SocialChannelConfig) -> PostVariant`

**Responsabilidades:**

- Dado un BasePost y una configuración de red social:
  - Construir un prompt con:
    - Contenido base.
    - Tipo de red.
    - Idioma es-ES.
    - Reglas técnicas obtenidas de un servicio de reglas hardcodeadas.
  - Llamar al proveedor de IA.
  - Recibir y validar el contenido generado.
  - Rellenar una PostVariant con estado `Generated` o `ReadyToPublish`.

> Las reglas técnicas (longitud, tipos de media, hashtags) estarán hardcodeadas en un servicio de configuración en código, no en BD.

#### 6.4.5. Servicio de publicación en redes

**Responsabilidades:**

- Para cada red social, implementar un método que:
  - Reciba una PostVariant.
  - Construya la llamada a la API de la red (usando `AccessToken` de la Configuración de red).
  - Procese el resultado y actualice el estado de la PostVariant (`Published` o `PublishError`).
  - Actualice `HealthStatus` de la Configuración de red si hay errores de autenticación/permisos.

> Se puede diseñar una interfaz `ISocialPublisher` y una implementación por red (ej. `InstagramPublisher`, `TikTokPublisher`, etc.).

#### 6.4.6. Servicio orquestador de batch

**Responsabilidades:**

- Ejecutar el flujo de adaptación IA para todas las Publicaciones base relevantes.
- Ejecutar el flujo de publicación para todas las Variantes `ReadyToPublish`.
- Actualizar estados de Publicaciones base en función de sus Variantes.

---

### 6.5. Procesos en background (Hosted Services)

Se recomiendan al menos dos Hosted Services:

1. **AdaptationHostedService**
   - Periodicidad: cada 180 minutos (3 horas).
   - Lógica:
     - Buscar Publicaciones base planificadas próximamente y generar sus Variantes.

2. **PublishingHostedService**
   - Periodicidad: cada 60 minutos (1 hora).
   - Lógica:
     - Buscar Variantes `ReadyToPublish` con `ScheduledAtUtc <= now(Utc)`.
     - Publicar en la red correspondiente.

Ambos respetan `IsEnabled` y `HealthStatus` de las Configuraciones de red.

---

### 6.6. Manejo de tiempos y zonas horarias

- Todas las fechas/hora persistidas en BD (`ScheduledAtUtc`, `PublishedAtUtc`, etc.) deben estar en **UTC**.
- La interfaz de usuario debe convertir de/ a la zona horaria del usuario (en el caso del usuario de referencia: `Europe/Madrid`).
- La comparación para “ScheduledAtUtc <= now” en los procesos de publicación se hace siempre en UTC.

---

### 6.7. Seguridad y almacenamiento de tokens

- Los campos `AccessToken` y `RefreshToken` deben almacenarse cifrados o protegidos.
- No se almacenan contraseñas de redes sociales.
- El flujo OAuth debe evitar exponer tokens en URLs de redirección.
- Se recomienda usar el sistema de protección de datos de ASP.NET Core o mecanismo equivalente para cifrar tokens en reposo.

---

### 6.8. Integración con IA (OpenRouter)

- Dejar encapsulado en un servicio de dominio la integración con OpenRouter.
- Modelo a utilizar: **Kimi k2 free** (a través de OpenRouter API).
- El servicio recibe:
  - Texto base.
  - Tipo de red.
  - Parámetros (longitud deseada, presencia de hashtags, etc.).
- El servicio devuelve el texto adaptado.
- En V1, el prompt estará hardcodeado en el servicio y contendrá las instrucciones en español (`es-ES`).
- Configuración:
  - API Key en appsettings (fake inicialmente, reemplazable).
  - Endpoint: https://openrouter.ai/api/v1/chat/completions
  - Model ID: "kimi-k2-free"

---

### 6.9. Validaciones técnicas mínimas

- Validar que las redes seleccionadas en una Publicación base corresponden a configuraciones de red con `IsEnabled = true`.
- Validar que al publicar se usa siempre una Configuración de red con `HealthStatus = OK`; si no, marcar error.
- Control básico de longitud de texto según reglas hardcodeadas por red (el servicio de reglas puede truncar o pedir al LLM un límite).

---

### 6.10. Requisitos no funcionales (básicos)

- La aplicación debe ser accesible desde navegadores modernos (Chrome, Edge, Firefox) en escritorio.
- El rendimiento esperado para el volumen inicial (decenas de Cuentas, cientos de Publicaciones) se considera bajo y no exige optimización avanzada en esta fase.
- Se debe registrar mínimo:
  - Errores en procesos de adaptación IA.
  - Errores en procesos de publicación.
  - Cambios de estado de redes (`HealthStatus`).

---

## 7. Resumen final

Este documento define:

- El **modelo de dominio** (Usuarios, Cuentas, Configuración de redes, Publicaciones base, Variantes).
- Los **flujos de negocio esenciales** (conexión OAuth, planificación, adaptación IA, publicación, gestión de errores).
- Los **requisitos técnicos** y una estructura de implementación en .NET + Blazor Server, con EF Core y Hosted Services.

Cualquier implementación que siga este documento deberá:

- Permitir gestionar negocios (Cuentas), sus redes activas y su calendario de publicaciones.
- Conectar redes mediante OAuth, guardando solo tokens.
- Generar variantes específicas por red usando IA en es-ES.
- Publicar automáticamente en las redes configuradas a la hora programada.
- Reflejar estados de éxito y error de forma coherente en el dominio.
