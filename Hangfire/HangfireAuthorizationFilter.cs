using Hangfire.Dashboard;

namespace SocialPanelCore.Hangfire;

/// <summary>
/// Filtro de autorización para el dashboard de Hangfire.
/// Solo permite acceso a usuarios autenticados.
/// </summary>
public class HangfireAuthorizationFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        var httpContext = context.GetHttpContext();
        return httpContext.User.Identity?.IsAuthenticated ?? false;
    }
}
