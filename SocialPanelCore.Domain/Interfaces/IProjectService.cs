using SocialPanelCore.Domain.Entities;

namespace SocialPanelCore.Domain.Interfaces;

public interface IProjectService
{
    Task<IEnumerable<Project>> GetAllProjectsAsync(Guid accountId);
    Task<Project?> GetProjectByIdAsync(Guid id);
    Task<Project> CreateProjectAsync(
        Guid accountId,
        string name,
        string? description,
        decimal? budget,
        DateTime? startDate,
        DateTime? endDate);
    Task UpdateProjectAsync(
        Guid id,
        string name,
        string? description,
        decimal? budget,
        DateTime? startDate,
        DateTime? endDate,
        bool isActive);
    Task DeleteProjectAsync(Guid id);
}
