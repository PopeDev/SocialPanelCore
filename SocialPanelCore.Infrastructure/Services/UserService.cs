using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SocialPanelCore.Domain.Entities;
using SocialPanelCore.Domain.Enums;
using SocialPanelCore.Domain.Interfaces;
using SocialPanelCore.Infrastructure.Data;

namespace SocialPanelCore.Infrastructure.Services;

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

        // Crear rol si no existe
        if (!await _roleManager.RoleExistsAsync(newRoleName))
        {
            await _roleManager.CreateAsync(new IdentityRole<Guid>(newRoleName));
        }

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
