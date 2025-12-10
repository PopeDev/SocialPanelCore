using SocialPanelCore.Domain.Entities;

namespace SocialPanelCore.Domain.Interfaces;

public interface IAccountService
{
    Task<IEnumerable<Account>> GetAllAccountsAsync();
    Task<Account?> GetAccountByIdAsync(Guid id);
    Task<Account> CreateAccountAsync(string name, string? description);
    Task<Account> UpdateAccountAsync(Guid id, string name, string? description);
    Task DeleteAccountAsync(Guid id);
}
