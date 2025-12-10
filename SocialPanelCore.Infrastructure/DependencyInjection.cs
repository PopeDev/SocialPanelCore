using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SocialPanelCore.Domain.Interfaces;
using SocialPanelCore.Infrastructure.Data;
using SocialPanelCore.Infrastructure.Services;

namespace SocialPanelCore.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<SocialPanelDbContext>(options =>
            options.UseNpgsql(connectionString));

        services.AddScoped<IAccountService, AccountService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IBasePostService, BasePostService>();
        services.AddScoped<ISocialChannelConfigService, SocialChannelConfigService>();

        return services;
    }
}
