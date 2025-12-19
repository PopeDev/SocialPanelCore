using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SocialPanelCore.Domain.Entities;
using SocialPanelCore.Domain.Interfaces;
using SocialPanelCore.Infrastructure.Data;

namespace SocialPanelCore.Infrastructure.Services;

public class ProjectService : IProjectService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<ProjectService> _logger;

    public ProjectService(ApplicationDbContext context, ILogger<ProjectService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<IEnumerable<Project>> GetAllProjectsAsync(Guid accountId)
    {
        _logger.LogInformation("Obteniendo proyectos de cuenta: {AccountId}", accountId);
        return await _context.Projects
            .AsNoTracking()
            .Where(p => p.AccountId == accountId)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();
    }

    public async Task<Project?> GetProjectByIdAsync(Guid id)
    {
        return await _context.Projects
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id);
    }

    public async Task<Project> CreateProjectAsync(
        Guid accountId,
        string name,
        string? description,
        decimal? budget,
        DateTime? startDate,
        DateTime? endDate)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("El nombre del proyecto es obligatorio", nameof(name));

        var accountExists = await _context.Accounts.AnyAsync(a => a.Id == accountId);
        if (!accountExists)
            throw new InvalidOperationException($"Cuenta no encontrada: {accountId}");

        var project = new Project
        {
            Id = Guid.NewGuid(),
            AccountId = accountId,
            Name = name.Trim(),
            Description = description?.Trim(),
            Budget = budget,
            StartDate = startDate,
            EndDate = endDate,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Projects.Add(project);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Proyecto creado: {ProjectId} - {ProjectName}", project.Id, project.Name);
        return project;
    }

    public async Task UpdateProjectAsync(
        Guid id,
        string name,
        string? description,
        decimal? budget,
        DateTime? startDate,
        DateTime? endDate,
        bool isActive)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("El nombre del proyecto es obligatorio", nameof(name));

        var project = await _context.Projects.FindAsync(id)
            ?? throw new InvalidOperationException($"Proyecto no encontrado: {id}");

        project.Name = name.Trim();
        project.Description = description?.Trim();
        project.Budget = budget;
        project.StartDate = startDate;
        project.EndDate = endDate;
        project.IsActive = isActive;
        project.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        _logger.LogInformation("Proyecto actualizado: {ProjectId}", id);
    }

    public async Task DeleteProjectAsync(Guid id)
    {
        var project = await _context.Projects
            .Include(p => p.Expenses)
            .Include(p => p.Reminders)
            .FirstOrDefaultAsync(p => p.Id == id)
            ?? throw new InvalidOperationException($"Proyecto no encontrado: {id}");

        _context.Projects.Remove(project);
        await _context.SaveChangesAsync();
        _logger.LogInformation("Proyecto eliminado: {ProjectId}", id);
    }
}
