# Reporte de Deuda Técnica - SocialPanelCore

**Fecha:** 2025-12-10
**Versión analizada:** Commit 8322ccb

---

## Resumen Ejecutivo

| Categoría | Cantidad | Gravedad |
|-----------|----------|----------|
| Servicios no implementados | 6 | **Crítica** |
| Interfaces sin definir | 6 | **Crítica** |
| Entidades sin definir | 4+ | **Crítica** |
| Enums sin definir | 4+ | **Crítica** |
| Implementaciones NoOp/Stub | 1 | Media |
| Trabajos Hangfire comentados | 2 | Media |

---

## 1. IAccountService

### Ubicación esperada
`SocialPanelCore.Infrastructure.Services.AccountService` implementando `SocialPanelCore.Domain.Interfaces.IAccountService`

### Registro en DI (Program.cs:87)
```csharp
builder.Services.AddScoped<IAccountService, AccountService>();
```

### Métodos utilizados en componentes

#### 1.1 GetAllAccountsAsync()
**Usado en:**
- `Components/Pages/Accounts/Index.razor:91`
- `Components/Pages/Publications/Index.razor:171`
- `Components/Pages/Publications/New.razor:269`
- `Components/Pages/SocialChannels/Index.razor:138`
- `Components/Pages/Reviews/Index.razor:111`

**Implementación recomendada:**
```csharp
public interface IAccountService
{
    Task<IEnumerable<Account>> GetAllAccountsAsync();
    Task<Account?> GetAccountByIdAsync(Guid id);
    Task<Account> CreateAccountAsync(string name, string? description);
    Task UpdateAccountAsync(Guid id, string name, string? description);
    Task DeleteAccountAsync(Guid id);
}

public class AccountService : IAccountService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<AccountService> _logger;

    public AccountService(ApplicationDbContext context, ILogger<AccountService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<IEnumerable<Account>> GetAllAccountsAsync()
    {
        _logger.LogInformation("Obteniendo todas las cuentas");
        return await _context.Accounts
            .AsNoTracking()
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync();
    }

    public async Task<Account?> GetAccountByIdAsync(Guid id)
    {
        return await _context.Accounts
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == id);
    }

    public async Task<Account> CreateAccountAsync(string name, string? description)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("El nombre de la cuenta es obligatorio", nameof(name));

        var account = new Account
        {
            Id = Guid.NewGuid(),
            Name = name.Trim(),
            Description = description?.Trim(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Accounts.Add(account);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Cuenta creada: {AccountId} - {AccountName}", account.Id, account.Name);
        return account;
    }

    public async Task UpdateAccountAsync(Guid id, string name, string? description)
    {
        var account = await _context.Accounts.FindAsync(id)
            ?? throw new InvalidOperationException($"Cuenta no encontrada: {id}");

        account.Name = name.Trim();
        account.Description = description?.Trim();
        account.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        _logger.LogInformation("Cuenta actualizada: {AccountId}", id);
    }

    public async Task DeleteAccountAsync(Guid id)
    {
        var account = await _context.Accounts
            .Include(a => a.SocialChannels)
            .Include(a => a.Posts)
            .FirstOrDefaultAsync(a => a.Id == id)
            ?? throw new InvalidOperationException($"Cuenta no encontrada: {id}");

        // Verificar dependencias
        if (account.Posts.Any())
        {
            throw new InvalidOperationException(
                $"No se puede eliminar la cuenta porque tiene {account.Posts.Count} publicaciones asociadas");
        }

        _context.Accounts.Remove(account);
        await _context.SaveChangesAsync();
        _logger.LogInformation("Cuenta eliminada: {AccountId}", id);
    }
}
```

---

## 2. IUserService

### Ubicación esperada
`SocialPanelCore.Infrastructure.Services.UserService` implementando `SocialPanelCore.Domain.Interfaces.IUserService`

### Registro en DI (Program.cs:89 - COMENTADO)
```csharp
// builder.Services.AddScoped<IUserService, UserService>();
```

### Métodos utilizados en componentes

**Usado en:**
- `Components/Pages/Users/Index.razor:99` - GetAllUsersAsync()
- `Components/Pages/Users/Index.razor:183` - DeleteUserAsync()
- `Components/Pages/Users/UserDialog.razor:115` - UpdateUserAsync()
- `Components/Pages/Users/UserDialog.razor:119` - CreateUserAsync()

**Implementación recomendada:**
```csharp
public interface IUserService
{
    Task<IEnumerable<User>> GetAllUsersAsync();
    Task<User?> GetUserByIdAsync(Guid id);
    Task<User> CreateUserAsync(string name, string email, UserRole role);
    Task UpdateUserAsync(Guid id, string name, string email, UserRole role);
    Task DeleteUserAsync(Guid id);
}

public class UserService : IUserService
{
    private readonly UserManager<User> _userManager;
    private readonly RoleManager<IdentityRole<Guid>> _roleManager;
    private readonly ApplicationDbContext _context;
    private readonly ILogger<UserService> _logger;

    public UserService(
        UserManager<User> userManager,
        RoleManager<IdentityRole<Guid>> roleManager,
        ApplicationDbContext context,
        ILogger<UserService> logger)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _context = context;
        _logger = logger;
    }

    public async Task<IEnumerable<User>> GetAllUsersAsync()
    {
        var users = await _context.Users
            .AsNoTracking()
            .OrderByDescending(u => u.CreatedAt)
            .ToListAsync();

        // Cargar roles para cada usuario
        foreach (var user in users)
        {
            var roles = await _userManager.GetRolesAsync(user);
            user.Role = roles.Contains("Superadministrador")
                ? UserRole.Superadministrador
                : UserRole.UsuarioBasico;
        }

        return users;
    }

    public async Task<User?> GetUserByIdAsync(Guid id)
    {
        return await _context.Users.FindAsync(id);
    }

    public async Task<User> CreateUserAsync(string name, string email, UserRole role)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("El nombre es obligatorio", nameof(name));
        if (string.IsNullOrWhiteSpace(email))
            throw new ArgumentException("El email es obligatorio", nameof(email));

        // Verificar email único
        var existingUser = await _userManager.FindByEmailAsync(email);
        if (existingUser != null)
            throw new InvalidOperationException("Ya existe un usuario con ese email");

        var user = new User
        {
            Id = Guid.NewGuid(),
            UserName = email,
            Email = email,
            Name = name.Trim(),
            Role = role,
            CreatedAt = DateTime.UtcNow,
            EmailConfirmed = true // Para desarrollo
        };

        // Generar password temporal
        var tempPassword = GenerateTemporaryPassword();
        var result = await _userManager.CreateAsync(user, tempPassword);

        if (!result.Succeeded)
        {
            var errors = string.Join(", ", result.Errors.Select(e => e.Description));
            throw new InvalidOperationException($"Error al crear usuario: {errors}");
        }

        // Asignar rol
        var roleName = role == UserRole.Superadministrador ? "Superadministrador" : "UsuarioBasico";

        // Crear rol si no existe
        if (!await _roleManager.RoleExistsAsync(roleName))
        {
            await _roleManager.CreateAsync(new IdentityRole<Guid>(roleName));
        }

        await _userManager.AddToRoleAsync(user, roleName);

        _logger.LogInformation("Usuario creado: {UserId} - {UserEmail} con rol {Role}",
            user.Id, user.Email, roleName);

        // TODO: Enviar email con credenciales temporales
        return user;
    }

    public async Task UpdateUserAsync(Guid id, string name, string email, UserRole role)
    {
        var user = await _userManager.FindByIdAsync(id.ToString())
            ?? throw new InvalidOperationException($"Usuario no encontrado: {id}");

        // Verificar email único (si cambió)
        if (user.Email != email)
        {
            var existingUser = await _userManager.FindByEmailAsync(email);
            if (existingUser != null && existingUser.Id != id)
                throw new InvalidOperationException("Ya existe otro usuario con ese email");

            user.Email = email;
            user.UserName = email;
            user.NormalizedEmail = email.ToUpperInvariant();
            user.NormalizedUserName = email.ToUpperInvariant();
        }

        user.Name = name.Trim();
        user.Role = role;

        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            var errors = string.Join(", ", result.Errors.Select(e => e.Description));
            throw new InvalidOperationException($"Error al actualizar usuario: {errors}");
        }

        // Actualizar roles
        var currentRoles = await _userManager.GetRolesAsync(user);
        await _userManager.RemoveFromRolesAsync(user, currentRoles);

        var newRoleName = role == UserRole.Superadministrador ? "Superadministrador" : "UsuarioBasico";
        await _userManager.AddToRoleAsync(user, newRoleName);

        _logger.LogInformation("Usuario actualizado: {UserId}", id);
    }

    public async Task DeleteUserAsync(Guid id)
    {
        var user = await _userManager.FindByIdAsync(id.ToString())
            ?? throw new InvalidOperationException($"Usuario no encontrado: {id}");

        // Verificar que no sea el último superadmin
        if (user.Role == UserRole.Superadministrador)
        {
            var superadminCount = await _context.Users
                .CountAsync(u => u.Role == UserRole.Superadministrador);

            if (superadminCount <= 1)
                throw new InvalidOperationException("No se puede eliminar el último superadministrador");
        }

        var result = await _userManager.DeleteAsync(user);
        if (!result.Succeeded)
        {
            var errors = string.Join(", ", result.Errors.Select(e => e.Description));
            throw new InvalidOperationException($"Error al eliminar usuario: {errors}");
        }

        _logger.LogInformation("Usuario eliminado: {UserId}", id);
    }

    private static string GenerateTemporaryPassword()
    {
        // Generar password que cumpla con las políticas
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghjkmnpqrstuvwxyz23456789";
        const string special = "!@#$%";
        var random = new Random();

        var password = new char[12];
        password[0] = chars[random.Next(26)]; // Mayúscula
        password[1] = chars[random.Next(26, 52)]; // Minúscula
        password[2] = chars[random.Next(52, chars.Length)]; // Número
        password[3] = special[random.Next(special.Length)]; // Especial

        for (int i = 4; i < 12; i++)
        {
            password[i] = chars[random.Next(chars.Length)];
        }

        // Mezclar
        return new string(password.OrderBy(_ => random.Next()).ToArray());
    }
}
```

---

## 3. IBasePostService

### Ubicación esperada
`SocialPanelCore.Infrastructure.Services.BasePostService` implementando `SocialPanelCore.Domain.Interfaces.IBasePostService`

### Registro en DI (Program.cs:91 - COMENTADO)
```csharp
// builder.Services.AddScoped<IBasePostService, BasePostService>();
```

### Métodos utilizados en componentes

**Usado en:**
- `Components/Pages/Publications/Index.razor:180` - GetPostsByAccountAsync()
- `Components/Pages/Publications/Index.razor:296` - DeletePostAsync()
- `Components/Pages/Publications/New.razor:337,399` - CreatePostAsync()
- `Components/Pages/Reviews/Index.razor:126` - GetPostsPendingReviewAsync()
- `Components/Pages/Reviews/ReviewDialog.razor:83` - ApprovePostAsync()
- `Components/Pages/Reviews/ReviewDialog.razor:87` - RejectPostAsync()

**Implementación recomendada:**
```csharp
public interface IBasePostService
{
    Task<IEnumerable<BasePost>> GetPostsByAccountAsync(Guid accountId);
    Task<IEnumerable<BasePost>> GetPostsPendingReviewAsync(Guid accountId);
    Task<BasePost?> GetPostByIdAsync(Guid id);
    Task<BasePost> CreatePostAsync(
        Guid accountId,
        Guid? createdByUserId,
        string content,
        DateTime scheduledAtUtc,
        List<NetworkType> targetNetworks,
        string? title = null,
        BasePostState initialState = BasePostState.Borrador);
    Task UpdatePostAsync(Guid id, string content, string? title, DateTime scheduledAtUtc);
    Task DeletePostAsync(Guid id);
    Task ApprovePostAsync(Guid postId, Guid approvedByUserId, string? notes);
    Task RejectPostAsync(Guid postId, Guid rejectedByUserId, string notes);
    Task ChangeStateAsync(Guid postId, BasePostState newState);
}

public class BasePostService : IBasePostService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<BasePostService> _logger;

    public BasePostService(ApplicationDbContext context, ILogger<BasePostService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<IEnumerable<BasePost>> GetPostsByAccountAsync(Guid accountId)
    {
        return await _context.BasePosts
            .AsNoTracking()
            .Include(p => p.TargetNetworks)
            .Include(p => p.CreatedByUser)
            .Where(p => p.AccountId == accountId)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<BasePost>> GetPostsPendingReviewAsync(Guid accountId)
    {
        // Posts en estado Planificada que requieren revisión antes de publicar
        return await _context.BasePosts
            .AsNoTracking()
            .Include(p => p.TargetNetworks)
            .Include(p => p.CreatedByUser)
            .Where(p => p.AccountId == accountId &&
                        p.State == BasePostState.Planificada &&
                        p.RequiresApproval == true)
            .OrderBy(p => p.ScheduledAtUtc)
            .ToListAsync();
    }

    public async Task<BasePost?> GetPostByIdAsync(Guid id)
    {
        return await _context.BasePosts
            .Include(p => p.TargetNetworks)
            .Include(p => p.Account)
            .Include(p => p.CreatedByUser)
            .FirstOrDefaultAsync(p => p.Id == id);
    }

    public async Task<BasePost> CreatePostAsync(
        Guid accountId,
        Guid? createdByUserId,
        string content,
        DateTime scheduledAtUtc,
        List<NetworkType> targetNetworks,
        string? title = null,
        BasePostState initialState = BasePostState.Borrador)
    {
        if (string.IsNullOrWhiteSpace(content))
            throw new ArgumentException("El contenido es obligatorio", nameof(content));

        if (!targetNetworks.Any())
            throw new ArgumentException("Debe seleccionar al menos una red social", nameof(targetNetworks));

        // Verificar que la cuenta existe
        var accountExists = await _context.Accounts.AnyAsync(a => a.Id == accountId);
        if (!accountExists)
            throw new InvalidOperationException($"Cuenta no encontrada: {accountId}");

        var post = new BasePost
        {
            Id = Guid.NewGuid(),
            AccountId = accountId,
            CreatedByUserId = createdByUserId,
            Title = title?.Trim(),
            Content = content.Trim(),
            ScheduledAtUtc = scheduledAtUtc.ToUniversalTime(),
            State = initialState,
            ContentType = DetermineContentType(content, targetNetworks),
            RequiresApproval = true, // Por defecto requiere aprobación
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        // Crear las redes objetivo
        post.TargetNetworks = targetNetworks.Select(nt => new PostTargetNetwork
        {
            Id = Guid.NewGuid(),
            BasePostId = post.Id,
            NetworkType = nt
        }).ToList();

        _context.BasePosts.Add(post);
        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "Post creado: {PostId} para cuenta {AccountId} con {NetworkCount} redes objetivo",
            post.Id, accountId, targetNetworks.Count);

        return post;
    }

    public async Task UpdatePostAsync(Guid id, string content, string? title, DateTime scheduledAtUtc)
    {
        var post = await _context.BasePosts.FindAsync(id)
            ?? throw new InvalidOperationException($"Post no encontrado: {id}");

        if (post.State == BasePostState.Publicada)
            throw new InvalidOperationException("No se puede editar un post ya publicado");

        post.Content = content.Trim();
        post.Title = title?.Trim();
        post.ScheduledAtUtc = scheduledAtUtc.ToUniversalTime();
        post.UpdatedAt = DateTime.UtcNow;

        // Si estaba adaptado, volver a estado pendiente de adaptación
        if (post.State == BasePostState.Adaptada)
        {
            post.State = BasePostState.AdaptacionPendiente;
        }

        await _context.SaveChangesAsync();
        _logger.LogInformation("Post actualizado: {PostId}", id);
    }

    public async Task DeletePostAsync(Guid id)
    {
        var post = await _context.BasePosts
            .Include(p => p.TargetNetworks)
            .Include(p => p.AdaptedVersions)
            .FirstOrDefaultAsync(p => p.Id == id)
            ?? throw new InvalidOperationException($"Post no encontrado: {id}");

        if (post.State == BasePostState.Publicada)
            throw new InvalidOperationException("No se puede eliminar un post ya publicado");

        _context.BasePosts.Remove(post);
        await _context.SaveChangesAsync();
        _logger.LogInformation("Post eliminado: {PostId}", id);
    }

    public async Task ApprovePostAsync(Guid postId, Guid approvedByUserId, string? notes)
    {
        var post = await _context.BasePosts.FindAsync(postId)
            ?? throw new InvalidOperationException($"Post no encontrado: {postId}");

        if (post.State != BasePostState.Planificada)
            throw new InvalidOperationException("Solo se pueden aprobar posts en estado Planificada");

        post.State = BasePostState.AdaptacionPendiente;
        post.ApprovedByUserId = approvedByUserId;
        post.ApprovedAt = DateTime.UtcNow;
        post.ApprovalNotes = notes;
        post.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        _logger.LogInformation("Post aprobado: {PostId} por usuario {UserId}", postId, approvedByUserId);
    }

    public async Task RejectPostAsync(Guid postId, Guid rejectedByUserId, string notes)
    {
        if (string.IsNullOrWhiteSpace(notes))
            throw new ArgumentException("Las notas son obligatorias al rechazar", nameof(notes));

        var post = await _context.BasePosts.FindAsync(postId)
            ?? throw new InvalidOperationException($"Post no encontrado: {postId}");

        if (post.State != BasePostState.Planificada)
            throw new InvalidOperationException("Solo se pueden rechazar posts en estado Planificada");

        post.State = BasePostState.Borrador;
        post.RejectedByUserId = rejectedByUserId;
        post.RejectedAt = DateTime.UtcNow;
        post.RejectionNotes = notes;
        post.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        _logger.LogInformation("Post rechazado: {PostId} por usuario {UserId}", postId, rejectedByUserId);
    }

    public async Task ChangeStateAsync(Guid postId, BasePostState newState)
    {
        var post = await _context.BasePosts.FindAsync(postId)
            ?? throw new InvalidOperationException($"Post no encontrado: {postId}");

        var oldState = post.State;
        post.State = newState;
        post.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        _logger.LogInformation("Post {PostId} cambió de estado: {OldState} -> {NewState}",
            postId, oldState, newState);
    }

    private static ContentType DetermineContentType(string content, List<NetworkType> networks)
    {
        // Lógica simple para determinar tipo de contenido
        // En producción esto sería más sofisticado
        if (networks.Any(n => n == NetworkType.TikTok || n == NetworkType.YouTube))
            return ContentType.Reel;

        if (networks.Any(n => n == NetworkType.Instagram) && content.Length < 100)
            return ContentType.Story;

        return ContentType.FeedPost;
    }
}
```

---

## 4. ISocialChannelConfigService

### Ubicación esperada
`SocialPanelCore.Infrastructure.Services.SocialChannelConfigService` implementando `SocialPanelCore.Domain.Interfaces.ISocialChannelConfigService`

### Registro en DI (Program.cs:90 - COMENTADO)
```csharp
// builder.Services.AddScoped<ISocialChannelConfigService, SocialChannelConfigService>();
```

### Métodos utilizados en componentes

**Usado en:**
- `Components/Pages/SocialChannels/Index.razor:156` - GetChannelConfigsByAccountAsync()
- `Components/Pages/SocialChannels/Index.razor:165` - DisableChannelAsync()
- `Components/Pages/SocialChannels/Index.razor:170` - EnableChannelAsync()

**Implementación recomendada:**
```csharp
public interface ISocialChannelConfigService
{
    Task<IEnumerable<SocialChannelConfig>> GetChannelConfigsByAccountAsync(Guid accountId);
    Task<SocialChannelConfig?> GetChannelConfigAsync(Guid id);
    Task<SocialChannelConfig> CreateChannelConfigAsync(
        Guid accountId,
        NetworkType networkType,
        string accessToken,
        string? refreshToken,
        DateTime? tokenExpiresAt,
        string? handle);
    Task UpdateTokensAsync(Guid id, string accessToken, string? refreshToken, DateTime? tokenExpiresAt);
    Task EnableChannelAsync(Guid id);
    Task DisableChannelAsync(Guid id);
    Task UpdateHealthStatusAsync(Guid id, HealthStatus status, string? errorMessage = null);
    Task DeleteChannelAsync(Guid id);
}

public class SocialChannelConfigService : ISocialChannelConfigService
{
    private readonly ApplicationDbContext _context;
    private readonly IDataProtector _protector;
    private readonly ILogger<SocialChannelConfigService> _logger;

    public SocialChannelConfigService(
        ApplicationDbContext context,
        IDataProtectionProvider dataProtectionProvider,
        ILogger<SocialChannelConfigService> logger)
    {
        _context = context;
        _protector = dataProtectionProvider.CreateProtector("SocialChannelTokens");
        _logger = logger;
    }

    public async Task<IEnumerable<SocialChannelConfig>> GetChannelConfigsByAccountAsync(Guid accountId)
    {
        var channels = await _context.SocialChannelConfigs
            .AsNoTracking()
            .Where(c => c.AccountId == accountId)
            .OrderBy(c => c.NetworkType)
            .ToListAsync();

        // No exponer tokens en la respuesta
        foreach (var channel in channels)
        {
            channel.AccessToken = "***PROTECTED***";
            channel.RefreshToken = null;
        }

        return channels;
    }

    public async Task<SocialChannelConfig?> GetChannelConfigAsync(Guid id)
    {
        return await _context.SocialChannelConfigs.FindAsync(id);
    }

    public async Task<SocialChannelConfig> CreateChannelConfigAsync(
        Guid accountId,
        NetworkType networkType,
        string accessToken,
        string? refreshToken,
        DateTime? tokenExpiresAt,
        string? handle)
    {
        // Verificar que la cuenta existe
        var accountExists = await _context.Accounts.AnyAsync(a => a.Id == accountId);
        if (!accountExists)
            throw new InvalidOperationException($"Cuenta no encontrada: {accountId}");

        // Verificar que no existe ya una configuración para esta red
        var existingConfig = await _context.SocialChannelConfigs
            .FirstOrDefaultAsync(c => c.AccountId == accountId && c.NetworkType == networkType);

        if (existingConfig != null)
            throw new InvalidOperationException(
                $"Ya existe una configuración de {networkType} para esta cuenta");

        var config = new SocialChannelConfig
        {
            Id = Guid.NewGuid(),
            AccountId = accountId,
            NetworkType = networkType,
            AccessToken = _protector.Protect(accessToken),
            RefreshToken = refreshToken != null ? _protector.Protect(refreshToken) : null,
            TokenExpiresAt = tokenExpiresAt,
            Handle = handle,
            IsEnabled = true,
            HealthStatus = HealthStatus.OK,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.SocialChannelConfigs.Add(config);
        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "Canal social configurado: {NetworkType} para cuenta {AccountId}",
            networkType, accountId);

        return config;
    }

    public async Task UpdateTokensAsync(Guid id, string accessToken, string? refreshToken, DateTime? tokenExpiresAt)
    {
        var config = await _context.SocialChannelConfigs.FindAsync(id)
            ?? throw new InvalidOperationException($"Configuración no encontrada: {id}");

        config.AccessToken = _protector.Protect(accessToken);
        config.RefreshToken = refreshToken != null ? _protector.Protect(refreshToken) : null;
        config.TokenExpiresAt = tokenExpiresAt;
        config.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        _logger.LogInformation("Tokens actualizados para canal {ChannelId}", id);
    }

    public async Task EnableChannelAsync(Guid id)
    {
        var config = await _context.SocialChannelConfigs.FindAsync(id)
            ?? throw new InvalidOperationException($"Configuración no encontrada: {id}");

        config.IsEnabled = true;
        config.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        _logger.LogInformation("Canal habilitado: {ChannelId}", id);
    }

    public async Task DisableChannelAsync(Guid id)
    {
        var config = await _context.SocialChannelConfigs.FindAsync(id)
            ?? throw new InvalidOperationException($"Configuración no encontrada: {id}");

        config.IsEnabled = false;
        config.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        _logger.LogInformation("Canal deshabilitado: {ChannelId}", id);
    }

    public async Task UpdateHealthStatusAsync(Guid id, HealthStatus status, string? errorMessage = null)
    {
        var config = await _context.SocialChannelConfigs.FindAsync(id)
            ?? throw new InvalidOperationException($"Configuración no encontrada: {id}");

        config.HealthStatus = status;
        config.LastHealthCheck = DateTime.UtcNow;
        config.LastErrorMessage = errorMessage;
        config.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
    }

    public async Task DeleteChannelAsync(Guid id)
    {
        var config = await _context.SocialChannelConfigs.FindAsync(id)
            ?? throw new InvalidOperationException($"Configuración no encontrada: {id}");

        _context.SocialChannelConfigs.Remove(config);
        await _context.SaveChangesAsync();
        _logger.LogInformation("Canal eliminado: {ChannelId}", id);
    }

    // Método interno para obtener token desprotegido (para uso en publicación)
    internal string GetDecryptedAccessToken(SocialChannelConfig config)
    {
        return _protector.Unprotect(config.AccessToken);
    }
}
```

---

## 5. IContentAdaptationService

### Ubicación esperada
`SocialPanelCore.Infrastructure.Services.ContentAdaptationService` implementando `SocialPanelCore.Domain.Interfaces.IContentAdaptationService`

### Registro en DI (Program.cs:92 - COMENTADO)
```csharp
// builder.Services.AddScoped<IContentAdaptationService, ContentAdaptationService>();
```

### Uso en Hangfire (Program.cs:157-160 - COMENTADO)
```csharp
// RecurringJob.AddOrUpdate<IContentAdaptationService>(
//     "adaptar-contenido-ia",
//     service => service.AdaptPendingPostsAsync(),
//     "0 */3 * * *"); // Cada 3 horas
```

**Implementación recomendada:**
```csharp
public interface IContentAdaptationService
{
    Task AdaptPendingPostsAsync();
    Task<AdaptedPost> AdaptPostForNetworkAsync(Guid basePostId, NetworkType network);
}

public class ContentAdaptationService : IContentAdaptationService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<ContentAdaptationService> _logger;
    // TODO: Inyectar cliente de IA (OpenAI, Claude, etc.)
    // private readonly IOpenAIService _openAIService;

    public ContentAdaptationService(
        ApplicationDbContext context,
        ILogger<ContentAdaptationService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task AdaptPendingPostsAsync()
    {
        _logger.LogInformation("Iniciando adaptación de posts pendientes");

        var pendingPosts = await _context.BasePosts
            .Include(p => p.TargetNetworks)
            .Include(p => p.AdaptedVersions)
            .Where(p => p.State == BasePostState.AdaptacionPendiente)
            .Take(10) // Procesar en lotes
            .ToListAsync();

        _logger.LogInformation("Encontrados {Count} posts pendientes de adaptación", pendingPosts.Count);

        foreach (var post in pendingPosts)
        {
            try
            {
                await AdaptPostAsync(post);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adaptando post {PostId}", post.Id);
                // Continuar con el siguiente post
            }
        }
    }

    private async Task AdaptPostAsync(BasePost post)
    {
        var networksToAdapt = post.TargetNetworks
            .Where(tn => !post.AdaptedVersions.Any(av => av.NetworkType == tn.NetworkType))
            .Select(tn => tn.NetworkType)
            .ToList();

        foreach (var network in networksToAdapt)
        {
            await AdaptPostForNetworkAsync(post.Id, network);
        }

        // Si todas las redes están adaptadas, cambiar estado
        if (networksToAdapt.Count == 0 ||
            post.TargetNetworks.All(tn =>
                post.AdaptedVersions.Any(av => av.NetworkType == tn.NetworkType)))
        {
            post.State = BasePostState.Adaptada;
            await _context.SaveChangesAsync();
        }
    }

    public async Task<AdaptedPost> AdaptPostForNetworkAsync(Guid basePostId, NetworkType network)
    {
        var basePost = await _context.BasePosts
            .Include(p => p.Account)
            .FirstOrDefaultAsync(p => p.Id == basePostId)
            ?? throw new InvalidOperationException($"Post no encontrado: {basePostId}");

        _logger.LogInformation(
            "Adaptando post {PostId} para red {Network}",
            basePostId, network);

        // Adaptar contenido según la red
        var adaptedContent = await GenerateAdaptedContentAsync(basePost, network);

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
            "Post adaptado: {AdaptedPostId} para red {Network}",
            adaptedPost.Id, network);

        return adaptedPost;
    }

    private async Task<string> GenerateAdaptedContentAsync(BasePost post, NetworkType network)
    {
        // TODO: Integrar con servicio de IA real
        // Por ahora, adaptación básica basada en reglas

        var content = post.Content;
        var maxLength = GetMaxLengthForNetwork(network);
        var tone = GetToneForNetwork(network);

        // Adaptación básica (placeholder para IA)
        var adapted = network switch
        {
            NetworkType.X => TruncateWithEllipsis(content, 280),
            NetworkType.LinkedIn => $"{content}\n\n#profesional #negocios",
            NetworkType.Instagram => $"{content}\n\n#instagram #socialmedia",
            NetworkType.TikTok => content.Length > 150
                ? TruncateWithEllipsis(content, 150)
                : content,
            NetworkType.Facebook => content,
            NetworkType.YouTube => $"{post.Title}\n\n{content}",
            _ => content
        };

        // Simular latencia de API de IA
        await Task.Delay(100);

        return adapted;
    }

    private static int GetMaxLengthForNetwork(NetworkType network)
    {
        return network switch
        {
            NetworkType.X => 280,
            NetworkType.Instagram => 2200,
            NetworkType.TikTok => 150,
            NetworkType.LinkedIn => 3000,
            NetworkType.Facebook => 63206,
            NetworkType.YouTube => 5000,
            _ => 1000
        };
    }

    private static string GetToneForNetwork(NetworkType network)
    {
        return network switch
        {
            NetworkType.LinkedIn => "profesional y formal",
            NetworkType.TikTok => "casual, juvenil y entretenido",
            NetworkType.Instagram => "visual y atractivo",
            NetworkType.X => "conciso y directo",
            _ => "neutral"
        };
    }

    private static string TruncateWithEllipsis(string text, int maxLength)
    {
        if (text.Length <= maxLength) return text;
        return text.Substring(0, maxLength - 3) + "...";
    }
}
```

---

## 6. ISocialPublisherService

### Ubicación esperada
`SocialPanelCore.Infrastructure.Services.SocialPublisherService` implementando `SocialPanelCore.Domain.Interfaces.ISocialPublisherService`

### Registro en DI (Program.cs:93 - COMENTADO)
```csharp
// builder.Services.AddScoped<ISocialPublisherService, SocialPublisherService>();
```

### Uso en Hangfire (Program.cs:162-165 - COMENTADO)
```csharp
// RecurringJob.AddOrUpdate<ISocialPublisherService>(
//     "publicar-posts-programados",
//     service => service.PublishScheduledPostsAsync(),
//     "*/5 * * * *"); // Cada 5 minutos
```

**Implementación recomendada:**
```csharp
public interface ISocialPublisherService
{
    Task PublishScheduledPostsAsync();
    Task<PublishResult> PublishToNetworkAsync(Guid adaptedPostId);
    Task RetryFailedPublicationsAsync();
}

public class SocialPublisherService : ISocialPublisherService
{
    private readonly ApplicationDbContext _context;
    private readonly SocialChannelConfigService _channelConfigService;
    private readonly ILogger<SocialPublisherService> _logger;
    // TODO: Inyectar clientes de APIs sociales
    // private readonly IFacebookClient _facebookClient;
    // private readonly IInstagramClient _instagramClient;
    // etc.

    public SocialPublisherService(
        ApplicationDbContext context,
        SocialChannelConfigService channelConfigService,
        ILogger<SocialPublisherService> logger)
    {
        _context = context;
        _channelConfigService = channelConfigService;
        _logger = logger;
    }

    public async Task PublishScheduledPostsAsync()
    {
        _logger.LogInformation("Iniciando publicación de posts programados");

        var now = DateTime.UtcNow;

        // Obtener posts adaptados listos para publicar
        var postsToPublish = await _context.BasePosts
            .Include(p => p.AdaptedVersions)
            .Include(p => p.Account)
                .ThenInclude(a => a.SocialChannels)
            .Where(p => p.State == BasePostState.Adaptada &&
                        p.ScheduledAtUtc <= now)
            .Take(20)
            .ToListAsync();

        _logger.LogInformation("Encontrados {Count} posts listos para publicar", postsToPublish.Count);

        foreach (var post in postsToPublish)
        {
            await PublishPostAsync(post);
        }
    }

    private async Task PublishPostAsync(BasePost post)
    {
        var successCount = 0;
        var failCount = 0;

        foreach (var adaptedPost in post.AdaptedVersions.Where(ap => ap.State == AdaptedPostState.Ready))
        {
            var result = await PublishToNetworkAsync(adaptedPost.Id);

            if (result.Success)
            {
                successCount++;
            }
            else
            {
                failCount++;
                _logger.LogWarning(
                    "Fallo publicando en {Network}: {Error}",
                    adaptedPost.NetworkType, result.ErrorMessage);
            }
        }

        // Actualizar estado del post base
        if (failCount == 0 && successCount > 0)
        {
            post.State = BasePostState.Publicada;
            post.PublishedAt = DateTime.UtcNow;
        }
        else if (successCount > 0)
        {
            post.State = BasePostState.ParcialmentePublicada;
        }

        await _context.SaveChangesAsync();
    }

    public async Task<PublishResult> PublishToNetworkAsync(Guid adaptedPostId)
    {
        var adaptedPost = await _context.AdaptedPosts
            .Include(ap => ap.BasePost)
                .ThenInclude(bp => bp.Account)
                    .ThenInclude(a => a.SocialChannels)
            .FirstOrDefaultAsync(ap => ap.Id == adaptedPostId)
            ?? throw new InvalidOperationException($"Post adaptado no encontrado: {adaptedPostId}");

        var channelConfig = adaptedPost.BasePost.Account.SocialChannels
            .FirstOrDefault(c => c.NetworkType == adaptedPost.NetworkType && c.IsEnabled);

        if (channelConfig == null)
        {
            return new PublishResult
            {
                Success = false,
                ErrorMessage = $"No hay canal configurado para {adaptedPost.NetworkType}"
            };
        }

        try
        {
            // Obtener token desencriptado
            var accessToken = _channelConfigService.GetDecryptedAccessToken(channelConfig);

            // Publicar según la red
            var externalId = adaptedPost.NetworkType switch
            {
                NetworkType.Facebook => await PublishToFacebookAsync(adaptedPost, accessToken),
                NetworkType.Instagram => await PublishToInstagramAsync(adaptedPost, accessToken),
                NetworkType.X => await PublishToXAsync(adaptedPost, accessToken),
                NetworkType.LinkedIn => await PublishToLinkedInAsync(adaptedPost, accessToken),
                NetworkType.TikTok => await PublishToTikTokAsync(adaptedPost, accessToken),
                NetworkType.YouTube => await PublishToYouTubeAsync(adaptedPost, accessToken),
                _ => throw new NotSupportedException($"Red no soportada: {adaptedPost.NetworkType}")
            };

            // Actualizar post adaptado
            adaptedPost.State = AdaptedPostState.Published;
            adaptedPost.PublishedAt = DateTime.UtcNow;
            adaptedPost.ExternalPostId = externalId;

            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Post publicado exitosamente en {Network}: {ExternalId}",
                adaptedPost.NetworkType, externalId);

            return new PublishResult { Success = true, ExternalId = externalId };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error publicando post {PostId} en {Network}",
                adaptedPostId, adaptedPost.NetworkType);

            adaptedPost.State = AdaptedPostState.Failed;
            adaptedPost.LastError = ex.Message;
            adaptedPost.RetryCount++;

            await _context.SaveChangesAsync();

            // Actualizar health status del canal
            await _channelConfigService.UpdateHealthStatusAsync(
                channelConfig.Id, HealthStatus.KO, ex.Message);

            return new PublishResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    public async Task RetryFailedPublicationsAsync()
    {
        var failedPosts = await _context.AdaptedPosts
            .Where(ap => ap.State == AdaptedPostState.Failed && ap.RetryCount < 3)
            .Take(10)
            .ToListAsync();

        foreach (var post in failedPosts)
        {
            await PublishToNetworkAsync(post.Id);
        }
    }

    // Métodos placeholder para cada red social
    // TODO: Implementar con SDKs reales de cada plataforma

    private Task<string> PublishToFacebookAsync(AdaptedPost post, string accessToken)
    {
        // TODO: Usar Facebook Graph API
        // POST https://graph.facebook.com/{page-id}/feed
        _logger.LogInformation("Publicando en Facebook (simulado)");
        return Task.FromResult($"fb_{Guid.NewGuid():N}");
    }

    private Task<string> PublishToInstagramAsync(AdaptedPost post, string accessToken)
    {
        // TODO: Usar Instagram Graph API
        // POST https://graph.facebook.com/{ig-user-id}/media
        _logger.LogInformation("Publicando en Instagram (simulado)");
        return Task.FromResult($"ig_{Guid.NewGuid():N}");
    }

    private Task<string> PublishToXAsync(AdaptedPost post, string accessToken)
    {
        // TODO: Usar Twitter/X API v2
        // POST https://api.twitter.com/2/tweets
        _logger.LogInformation("Publicando en X (simulado)");
        return Task.FromResult($"x_{Guid.NewGuid():N}");
    }

    private Task<string> PublishToLinkedInAsync(AdaptedPost post, string accessToken)
    {
        // TODO: Usar LinkedIn Marketing API
        // POST https://api.linkedin.com/v2/ugcPosts
        _logger.LogInformation("Publicando en LinkedIn (simulado)");
        return Task.FromResult($"li_{Guid.NewGuid():N}");
    }

    private Task<string> PublishToTikTokAsync(AdaptedPost post, string accessToken)
    {
        // TODO: Usar TikTok Content Posting API
        // POST https://open.tiktokapis.com/v2/post/publish/content/init/
        _logger.LogInformation("Publicando en TikTok (simulado)");
        return Task.FromResult($"tt_{Guid.NewGuid():N}");
    }

    private Task<string> PublishToYouTubeAsync(AdaptedPost post, string accessToken)
    {
        // TODO: Usar YouTube Data API v3
        // POST https://www.googleapis.com/upload/youtube/v3/videos
        _logger.LogInformation("Publicando en YouTube (simulado)");
        return Task.FromResult($"yt_{Guid.NewGuid():N}");
    }
}

public class PublishResult
{
    public bool Success { get; set; }
    public string? ExternalId { get; set; }
    public string? ErrorMessage { get; set; }
}
```

---

## 7. IdentityNoOpEmailSender (Stub existente)

### Ubicación actual
`Components/Account/IdentityNoOpEmailSender.cs`

### Estado actual
Es un stub que **no envía emails realmente**. Solo simula el envío.

### Problema
El comentario en el código indica: *"Remove the 'else if (EmailSender is IdentityNoOpEmailSender)' block from RegisterConfirmation.razor after updating with a real implementation"*

**Implementación recomendada (usando SMTP):**
```csharp
public class SmtpEmailSender : IEmailSender<User>
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<SmtpEmailSender> _logger;

    public SmtpEmailSender(IConfiguration configuration, ILogger<SmtpEmailSender> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task SendConfirmationLinkAsync(User user, string email, string confirmationLink)
    {
        var subject = "Confirma tu cuenta en SocialPanelCore";
        var htmlBody = $@"
            <h2>Bienvenido a SocialPanelCore</h2>
            <p>Hola {user.Name},</p>
            <p>Por favor confirma tu cuenta haciendo clic en el siguiente enlace:</p>
            <p><a href='{confirmationLink}' style='padding: 10px 20px; background-color: #007bff; color: white; text-decoration: none; border-radius: 5px;'>Confirmar Email</a></p>
            <p>Si no creaste esta cuenta, puedes ignorar este mensaje.</p>
            <hr>
            <p><small>Este es un mensaje automático, por favor no respondas a este correo.</small></p>
        ";

        await SendEmailAsync(email, subject, htmlBody);
    }

    public async Task SendPasswordResetLinkAsync(User user, string email, string resetLink)
    {
        var subject = "Restablecer contraseña - SocialPanelCore";
        var htmlBody = $@"
            <h2>Restablecer Contraseña</h2>
            <p>Hola {user.Name},</p>
            <p>Hemos recibido una solicitud para restablecer tu contraseña.</p>
            <p><a href='{resetLink}' style='padding: 10px 20px; background-color: #dc3545; color: white; text-decoration: none; border-radius: 5px;'>Restablecer Contraseña</a></p>
            <p>Este enlace expirará en 24 horas.</p>
            <p>Si no solicitaste este cambio, puedes ignorar este mensaje.</p>
        ";

        await SendEmailAsync(email, subject, htmlBody);
    }

    public async Task SendPasswordResetCodeAsync(User user, string email, string resetCode)
    {
        var subject = "Código de restablecimiento - SocialPanelCore";
        var htmlBody = $@"
            <h2>Código de Restablecimiento</h2>
            <p>Hola {user.Name},</p>
            <p>Tu código de restablecimiento de contraseña es:</p>
            <p style='font-size: 24px; font-weight: bold; padding: 10px; background-color: #f0f0f0; text-align: center;'>{resetCode}</p>
            <p>Este código expirará en 15 minutos.</p>
        ";

        await SendEmailAsync(email, subject, htmlBody);
    }

    private async Task SendEmailAsync(string to, string subject, string htmlBody)
    {
        var smtpHost = _configuration["Email:SmtpHost"]
            ?? throw new InvalidOperationException("Email:SmtpHost no configurado");
        var smtpPort = int.Parse(_configuration["Email:SmtpPort"] ?? "587");
        var smtpUser = _configuration["Email:SmtpUser"]
            ?? throw new InvalidOperationException("Email:SmtpUser no configurado");
        var smtpPassword = _configuration["Email:SmtpPassword"]
            ?? throw new InvalidOperationException("Email:SmtpPassword no configurado");
        var fromEmail = _configuration["Email:FromEmail"] ?? smtpUser;
        var fromName = _configuration["Email:FromName"] ?? "SocialPanelCore";

        using var message = new MimeMessage();
        message.From.Add(new MailboxAddress(fromName, fromEmail));
        message.To.Add(MailboxAddress.Parse(to));
        message.Subject = subject;

        var builder = new BodyBuilder { HtmlBody = htmlBody };
        message.Body = builder.ToMessageBody();

        using var client = new SmtpClient();

        try
        {
            await client.ConnectAsync(smtpHost, smtpPort, MailKit.Security.SecureSocketOptions.StartTls);
            await client.AuthenticateAsync(smtpUser, smtpPassword);
            await client.SendAsync(message);
            await client.DisconnectAsync(true);

            _logger.LogInformation("Email enviado exitosamente a {To}", to);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error enviando email a {To}", to);
            throw;
        }
    }
}
```

**Configuración requerida en appsettings.json:**
```json
{
  "Email": {
    "SmtpHost": "smtp.gmail.com",
    "SmtpPort": 587,
    "SmtpUser": "tu-email@gmail.com",
    "SmtpPassword": "app-password",
    "FromEmail": "noreply@socialpanelcore.com",
    "FromName": "SocialPanelCore"
  }
}
```

---

## 8. Entidades Faltantes

Las siguientes entidades son referenciadas pero no existen en el proyecto:

### 8.1 Account
```csharp
namespace SocialPanelCore.Domain.Entities
{
    public class Account
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        // Navegación
        public virtual ICollection<SocialChannelConfig> SocialChannels { get; set; } = new List<SocialChannelConfig>();
        public virtual ICollection<BasePost> Posts { get; set; } = new List<BasePost>();
        public virtual ICollection<UserAccountAccess> UserAccess { get; set; } = new List<UserAccountAccess>();
    }
}
```

### 8.2 User (extender IdentityUser)
```csharp
namespace SocialPanelCore.Domain.Entities
{
    public class User : IdentityUser<Guid>
    {
        public string Name { get; set; } = string.Empty;
        public UserRole Role { get; set; } = UserRole.UsuarioBasico;
        public DateTime CreatedAt { get; set; }

        // Navegación
        public virtual ICollection<BasePost> CreatedPosts { get; set; } = new List<BasePost>();
        public virtual ICollection<UserAccountAccess> AccountAccess { get; set; } = new List<UserAccountAccess>();
    }
}
```

### 8.3 BasePost
```csharp
namespace SocialPanelCore.Domain.Entities
{
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
    }
}
```

### 8.4 SocialChannelConfig
```csharp
namespace SocialPanelCore.Domain.Entities
{
    public class SocialChannelConfig
    {
        public Guid Id { get; set; }
        public Guid AccountId { get; set; }
        public NetworkType NetworkType { get; set; }
        public string AccessToken { get; set; } = string.Empty; // Encriptado
        public string? RefreshToken { get; set; } // Encriptado
        public DateTime? TokenExpiresAt { get; set; }
        public string? Handle { get; set; } // @usuario o nombre de página
        public bool IsEnabled { get; set; }
        public HealthStatus HealthStatus { get; set; }
        public DateTime? LastHealthCheck { get; set; }
        public string? LastErrorMessage { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        // Navegación
        public virtual Account Account { get; set; } = null!;
    }
}
```

### 8.5 PostTargetNetwork
```csharp
namespace SocialPanelCore.Domain.Entities
{
    public class PostTargetNetwork
    {
        public Guid Id { get; set; }
        public Guid BasePostId { get; set; }
        public NetworkType NetworkType { get; set; }

        // Navegación
        public virtual BasePost BasePost { get; set; } = null!;
    }
}
```

### 8.6 AdaptedPost
```csharp
namespace SocialPanelCore.Domain.Entities
{
    public class AdaptedPost
    {
        public Guid Id { get; set; }
        public Guid BasePostId { get; set; }
        public NetworkType NetworkType { get; set; }
        public string AdaptedContent { get; set; } = string.Empty;
        public int CharacterCount { get; set; }
        public AdaptedPostState State { get; set; }
        public DateTime? PublishedAt { get; set; }
        public string? ExternalPostId { get; set; }
        public string? LastError { get; set; }
        public int RetryCount { get; set; }
        public DateTime CreatedAt { get; set; }

        // Navegación
        public virtual BasePost BasePost { get; set; } = null!;
    }
}
```

---

## 9. Enums Faltantes

### 9.1 UserRole
```csharp
namespace SocialPanelCore.Domain.Enums
{
    public enum UserRole
    {
        UsuarioBasico = 0,
        Superadministrador = 1
    }
}
```

### 9.2 NetworkType
```csharp
namespace SocialPanelCore.Domain.Enums
{
    public enum NetworkType
    {
        Facebook = 0,
        Instagram = 1,
        TikTok = 2,
        X = 3,      // Twitter
        YouTube = 4,
        LinkedIn = 5
    }
}
```

### 9.3 BasePostState
```csharp
namespace SocialPanelCore.Domain.Enums
{
    public enum BasePostState
    {
        Borrador = 0,
        Planificada = 1,
        AdaptacionPendiente = 2,
        Adaptada = 3,
        ParcialmentePublicada = 4,
        Publicada = 5,
        Cancelada = 6
    }
}
```

### 9.4 ContentType
```csharp
namespace SocialPanelCore.Domain.Enums
{
    public enum ContentType
    {
        FeedPost = 0,
        Story = 1,
        Reel = 2
    }
}
```

### 9.5 HealthStatus
```csharp
namespace SocialPanelCore.Domain.Enums
{
    public enum HealthStatus
    {
        OK = 0,
        KO = 1
    }
}
```

### 9.6 AdaptedPostState
```csharp
namespace SocialPanelCore.Domain.Enums
{
    public enum AdaptedPostState
    {
        Pending = 0,
        Ready = 1,
        Published = 2,
        Failed = 3
    }
}
```

---

## 10. ApplicationDbContext Recomendado

```csharp
namespace SocialPanelCore.Infrastructure.Data
{
    public class ApplicationDbContext : IdentityDbContext<User, IdentityRole<Guid>, Guid>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<Account> Accounts => Set<Account>();
        public DbSet<SocialChannelConfig> SocialChannelConfigs => Set<SocialChannelConfig>();
        public DbSet<BasePost> BasePosts => Set<BasePost>();
        public DbSet<PostTargetNetwork> PostTargetNetworks => Set<PostTargetNetwork>();
        public DbSet<AdaptedPost> AdaptedPosts => Set<AdaptedPost>();
        public DbSet<UserAccountAccess> UserAccountAccess => Set<UserAccountAccess>();

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // Configuración de Account
            builder.Entity<Account>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
                entity.Property(e => e.Description).HasMaxLength(1000);
            });

            // Configuración de SocialChannelConfig
            builder.Entity<SocialChannelConfig>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => new { e.AccountId, e.NetworkType }).IsUnique();
                entity.Property(e => e.AccessToken).IsRequired();
                entity.HasOne(e => e.Account)
                    .WithMany(a => a.SocialChannels)
                    .HasForeignKey(e => e.AccountId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // Configuración de BasePost
            builder.Entity<BasePost>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Content).IsRequired();
                entity.Property(e => e.Title).HasMaxLength(200);
                entity.HasOne(e => e.Account)
                    .WithMany(a => a.Posts)
                    .HasForeignKey(e => e.AccountId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(e => e.CreatedByUser)
                    .WithMany(u => u.CreatedPosts)
                    .HasForeignKey(e => e.CreatedByUserId)
                    .OnDelete(DeleteBehavior.SetNull);
            });

            // Configuración de PostTargetNetwork
            builder.Entity<PostTargetNetwork>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasOne(e => e.BasePost)
                    .WithMany(p => p.TargetNetworks)
                    .HasForeignKey(e => e.BasePostId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // Configuración de AdaptedPost
            builder.Entity<AdaptedPost>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => new { e.BasePostId, e.NetworkType }).IsUnique();
                entity.HasOne(e => e.BasePost)
                    .WithMany(p => p.AdaptedVersions)
                    .HasForeignKey(e => e.BasePostId)
                    .OnDelete(DeleteBehavior.Cascade);
            });
        }
    }
}
```

---

## 11. Estructura de Carpetas Recomendada

```
SocialPanelCore/
├── Domain/
│   ├── Entities/
│   │   ├── Account.cs
│   │   ├── User.cs
│   │   ├── BasePost.cs
│   │   ├── AdaptedPost.cs
│   │   ├── SocialChannelConfig.cs
│   │   ├── PostTargetNetwork.cs
│   │   └── UserAccountAccess.cs
│   ├── Enums/
│   │   ├── UserRole.cs
│   │   ├── NetworkType.cs
│   │   ├── BasePostState.cs
│   │   ├── AdaptedPostState.cs
│   │   ├── ContentType.cs
│   │   └── HealthStatus.cs
│   └── Interfaces/
│       ├── IAccountService.cs
│       ├── IUserService.cs
│       ├── IBasePostService.cs
│       ├── ISocialChannelConfigService.cs
│       ├── IContentAdaptationService.cs
│       └── ISocialPublisherService.cs
├── Infrastructure/
│   ├── Data/
│   │   └── ApplicationDbContext.cs
│   └── Services/
│       ├── AccountService.cs
│       ├── UserService.cs
│       ├── BasePostService.cs
│       ├── SocialChannelConfigService.cs
│       ├── ContentAdaptationService.cs
│       ├── SocialPublisherService.cs
│       └── SmtpEmailSender.cs
└── ...
```

---

## 12. Plan de Acción Recomendado

### Fase 1: Fundamentos (Prioridad Alta)
1. Crear estructura de carpetas Domain/Infrastructure
2. Implementar todas las entidades
3. Implementar todos los enums
4. Crear ApplicationDbContext con configuraciones
5. Generar migración inicial

### Fase 2: Servicios Core (Prioridad Alta)
1. Implementar IAccountService y AccountService
2. Implementar IUserService y UserService
3. Registrar servicios en Program.cs
4. Probar CRUD básico

### Fase 3: Publicaciones (Prioridad Media)
1. Implementar IBasePostService y BasePostService
2. Implementar ISocialChannelConfigService
3. Probar flujo de creación de posts

### Fase 4: Automatización (Prioridad Media)
1. Implementar IContentAdaptationService
2. Implementar ISocialPublisherService
3. Habilitar trabajos de Hangfire
4. Probar flujo completo de publicación

### Fase 5: Integraciones (Prioridad Baja)
1. Integrar APIs reales de redes sociales
2. Implementar flujos OAuth reales
3. Implementar servicio de email real

---

## 13. TODOs adicionales encontrados en el código

| Ubicación | TODO | Prioridad |
|-----------|------|-----------|
| `SocialChannels/Index.razor:184` | "Implementar flujo OAuth real" | Alta |
| `Reviews/Index.razor:177` | "Navegar a vista detallada" | Baja |
| `Program.cs:88` | "Implementar servicios restantes" | Alta |
| `Program.cs:156` | "Descomentar cuando se implementen los servicios" | Media |

---

**Nota:** Este documento representa el estado de la deuda técnica a fecha 2025-12-10. Se recomienda actualizar conforme se vayan implementando los servicios.
