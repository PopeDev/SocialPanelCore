using Microsoft.EntityFrameworkCore;
using SocialPanelCore.Domain.Entities;
using SocialPanelCore.Domain.Interfaces;
using SocialPanelCore.Infrastructure.Data;

namespace SocialPanelCore.Infrastructure.Services;

public class AccountService : IAccountService
{
    private readonly SocialPanelDbContext _context;

    public AccountService(SocialPanelDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<Account>> GetAllAccountsAsync()
    {
        return await _context.Accounts
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync();
    }

    public async Task<Account?> GetAccountByIdAsync(Guid id)
    {
        return await _context.Accounts.FindAsync(id);
    }

    public async Task<Account> CreateAccountAsync(string name, string? description)
    {
        var account = new Account
        {
            Id = Guid.NewGuid(),
            Name = name,
            Description = description,
            CreatedAt = DateTime.UtcNow
        };

        _context.Accounts.Add(account);
        await _context.SaveChangesAsync();

        return account;
    }

    public async Task<Account> UpdateAccountAsync(Guid id, string name, string? description)
    {
        var account = await _context.Accounts.FindAsync(id)
            ?? throw new InvalidOperationException($"Account with id {id} not found");

        account.Name = name;
        account.Description = description;
        account.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return account;
    }

    public async Task DeleteAccountAsync(Guid id)
    {
        var account = await _context.Accounts.FindAsync(id)
            ?? throw new InvalidOperationException($"Account with id {id} not found");

        _context.Accounts.Remove(account);
        await _context.SaveChangesAsync();
    }
}
