using Microsoft.EntityFrameworkCore;
using SocialPanelCore.Domain.Entities;
using SocialPanelCore.Domain.Enums;
using SocialPanelCore.Domain.Interfaces;
using SocialPanelCore.Infrastructure.Data;

namespace SocialPanelCore.Infrastructure.Services;

public class UserService : IUserService
{
    private readonly SocialPanelDbContext _context;

    public UserService(SocialPanelDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<User>> GetAllUsersAsync()
    {
        return await _context.Users
            .OrderByDescending(u => u.CreatedAt)
            .ToListAsync();
    }

    public async Task<User?> GetUserByIdAsync(Guid id)
    {
        return await _context.Users.FindAsync(id);
    }

    public async Task<User> CreateUserAsync(string name, string email, UserRole role)
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Name = name,
            Email = email,
            Role = role,
            CreatedAt = DateTime.UtcNow
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        return user;
    }

    public async Task<User> UpdateUserAsync(Guid id, string name, string email, UserRole role)
    {
        var user = await _context.Users.FindAsync(id)
            ?? throw new InvalidOperationException($"User with id {id} not found");

        user.Name = name;
        user.Email = email;
        user.Role = role;
        user.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return user;
    }

    public async Task DeleteUserAsync(Guid id)
    {
        var user = await _context.Users.FindAsync(id)
            ?? throw new InvalidOperationException($"User with id {id} not found");

        _context.Users.Remove(user);
        await _context.SaveChangesAsync();
    }
}
