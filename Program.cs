using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MudBlazor.Services;
using Serilog;
using SocialPanelCore.Infrastructure.Services;
using SocialPanelCore.Components;
using SocialPanelCore.Components.Account;
using SocialPanelCore.Domain.Interfaces;
using SocialPanelCore.Infrastructure.Data;
using SocialPanelCore.Domain.Entities;
using SocialPanelCore.Hangfire;
using Hangfire;
using Hangfire.PostgreSql;

// Configurar Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(new ConfigurationBuilder()
        .AddJsonFile("appsettings.json")
        .Build())
    .CreateLogger();

try
{
    Log.Information("Starting SocialPanelCore application");

    var builder = WebApplication.CreateBuilder(args);

    // Usar Serilog como logger
    builder.Host.UseSerilog();

    // Add services to the container.
    builder.Services.AddRazorComponents()
        .AddInteractiveServerComponents();

    // MudBlazor
    builder.Services.AddMudServices();

    // Add HttpContextAccessor for cascading HttpContext in Blazor components
    builder.Services.AddHttpContextAccessor();

    builder.Services.AddCascadingAuthenticationState();
    builder.Services.AddScoped<IdentityRedirectManager>();
    builder.Services.AddScoped<AuthenticationStateProvider, IdentityRevalidatingAuthenticationStateProvider>();

    builder.Services.AddAuthentication(options =>
        {
            options.DefaultScheme = IdentityConstants.ApplicationScheme;
            options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
        })
        .AddIdentityCookies();

    // PostgreSQL Database Context - ApplicationDbContext único para todo
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

    builder.Services.AddDbContext<ApplicationDbContext>(options =>
        options.UseNpgsql(connectionString));

    builder.Services.AddDatabaseDeveloperPageExceptionFilter();

    // ASP.NET Core Identity
    builder.Services.AddIdentityCore<User>(options =>
        {
            options.SignIn.RequireConfirmedAccount = false; // Para desarrollo
            options.Password.RequireDigit = true;
            options.Password.RequireLowercase = true;
            options.Password.RequireUppercase = true;
            options.Password.RequireNonAlphanumeric = false;
            options.Password.RequiredLength = 6;
            // Configurar SchemaVersion para soportar passkeys (Identity v3+)
            options.Stores.SchemaVersion = IdentitySchemaVersions.Version3;
            // Permitir login con email
            options.User.RequireUniqueEmail = true;
        })
        .AddRoles<IdentityRole<Guid>>()
        .AddEntityFrameworkStores<ApplicationDbContext>()
        .AddSignInManager()
        .AddDefaultTokenProviders();

    builder.Services.AddSingleton<IEmailSender<User>, IdentityNoOpEmailSender>();

    // Data Protection API para cifrado de tokens
    builder.Services.AddDataProtection();

    // Servicios de Application
    builder.Services.AddScoped<IAccountService, AccountService>();
    builder.Services.AddScoped<IUserService, UserService>();
    builder.Services.AddScoped<ISocialChannelConfigService, SocialChannelConfigService>();
    builder.Services.AddScoped<IBasePostService, BasePostService>();
    builder.Services.AddScoped<IContentAdaptationService, ContentAdaptationService>();
    builder.Services.AddScoped<SocialChannelConfigService>(); // Necesario para SocialPublisherService
    builder.Services.AddScoped<ISocialPublisherService, SocialPublisherService>();

    // Configurar Hangfire con PostgreSQL para trabajos en background
    builder.Services.AddHangfire(config =>
    {
        config.SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings()
            .UsePostgreSqlStorage(options =>
                options.UseNpgsqlConnection(connectionString));
    });

    // Agregar servidor de Hangfire para procesamiento de trabajos
    builder.Services.AddHangfireServer(options =>
    {
        options.ServerName = "SocialPanelCore-Worker";
        options.WorkerCount = 5; // Número de workers paralelos
    });

    var app = builder.Build();

    // Aplicar migraciones automáticamente en desarrollo
    using (var scope = app.Services.CreateScope())
    {
        var services = scope.ServiceProvider;
        try
        {
            var context = services.GetRequiredService<ApplicationDbContext>();
            context.Database.Migrate();
            Log.Information("Database migrations applied successfully");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "An error occurred while migrating the database");
            throw;
        }
    }

    // Configure the HTTP request pipeline.
    if (app.Environment.IsDevelopment())
    {
        app.UseMigrationsEndPoint();
    }
    else
    {
        app.UseExceptionHandler("/Error", createScopeForErrors: true);
        app.UseHsts();
    }

    app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
    app.UseHttpsRedirection();

    // Servir archivos estáticos (incluido mediavault)
    app.UseStaticFiles();

    app.UseAntiforgery();

    // Serilog request logging
    app.UseSerilogRequestLogging();

    // Hangfire Dashboard - Accesible en /hangfire (solo usuarios autenticados)
    app.UseHangfireDashboard("/hangfire", new DashboardOptions
    {
        Authorization = new[] { new HangfireAuthorizationFilter() },
        DashboardTitle = "SocialPanelCore - Trabajos en Background"
    });

    app.MapStaticAssets();
    app.MapRazorComponents<App>()
        .AddInteractiveServerRenderMode();

    // Add additional endpoints required by the Identity /Account Razor components.
    app.MapAdditionalIdentityEndpoints();

    // Crear carpeta de logs si no existe
    var logsPath = Path.Combine(Directory.GetCurrentDirectory(), "logs");
    if (!Directory.Exists(logsPath))
        Directory.CreateDirectory(logsPath);

    // Configurar trabajos recurrentes de Hangfire
    RecurringJob.AddOrUpdate<IContentAdaptationService>(
        "adaptar-contenido-ia",
        service => service.AdaptPendingPostsAsync(),
        "0 */3 * * *"); // Cada 3 horas

    RecurringJob.AddOrUpdate<ISocialPublisherService>(
        "publicar-posts-programados",
        service => service.PublishScheduledPostsAsync(),
        "*/5 * * * *"); // Cada 5 minutos

    Log.Information("SocialPanelCore application started successfully");
    Log.Information("Hangfire Dashboard disponible en: /hangfire");

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
