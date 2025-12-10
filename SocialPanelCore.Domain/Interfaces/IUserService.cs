using SocialPanelCore.Domain.Entities;
using SocialPanelCore.Domain.Enums;

namespace SocialPanelCore.Domain.Interfaces;

public interface IUserService
{
    Task<IEnumerable<User>> GetAllUsersAsync();
    Task<User?> GetUserByIdAsync(Guid id);
    Task<User> CreateUserAsync(string name, string email, UserRole role);
    Task<User> UpdateUserAsync(Guid id, string name, string email, UserRole role);
    Task DeleteUserAsync(Guid id);
}
