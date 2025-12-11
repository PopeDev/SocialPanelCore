using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SocialPanelCore.Domain.Entities;
using SocialPanelCore.Domain.Interfaces;
using SocialPanelCore.Infrastructure.Data;

namespace SocialPanelCore.Infrastructure.Services;

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
