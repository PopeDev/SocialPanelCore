using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using MudBlazor.Services;
using Refit;
using Serilog;
using SocialPanelCore.Infrastructure.Services;
using SocialPanelCore.Components;
using SocialPanelCore.Components.Account;
using SocialPanelCore.Domain.Configuration;
using SocialPanelCore.Domain.Interfaces;
using SocialPanelCore.Infrastructure.Data;
using SocialPanelCore.Domain.Entities;
using SocialPanelCore.Hangfire;
using SocialPanelCore.Infrastructure.ExternalApis.X;
using SocialPanelCore.Infrastructure.ExternalApis.Meta;
using SocialPanelCore.Infrastructure.ExternalApis.TikTok;
using SocialPanelCore.Infrastructure.ExternalApis.YouTube;
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

    // Añadir controladores MVC para endpoints OAuth
    builder.Services.AddControllers();

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

    // Configuración de almacenamiento de medios
    builder.Services.Configure<StorageSettings>(
        builder.Configuration.GetSection(StorageSettings.SectionName));

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

    // HttpClient para llamadas a APIs externas (OAuth, etc.)
    builder.Services.AddHttpClient();

    // Servicios de Application
    builder.Services.AddScoped<IAccountService, AccountService>();
    builder.Services.AddScoped<IUserService, UserService>();
    builder.Services.AddScoped<ISocialChannelConfigService, SocialChannelConfigService>();
    builder.Services.AddScoped<IBasePostService, BasePostService>();
    builder.Services.AddScoped<IContentAdaptationService, ContentAdaptationService>();
    builder.Services.AddScoped<ISocialPublisherService, SocialPublisherService>();
    builder.Services.AddScoped<IOAuthService, OAuthService>();
    builder.Services.AddScoped<IMediaStorageService, MediaStorageService>();

    // Servicios de IA y publicacion inmediata (Sprint 4)
    builder.Services.AddScoped<IAiContentService, AiContentService>();
    builder.Services.AddScoped<IImmediatePublishService, ImmediatePublishService>();

    // Sprint 6: OAuth multi-tenant con PKCE y renovación automática
    builder.Services.AddScoped<IOAuthStateStore, OAuthStateStore>();
    builder.Services.AddScoped<ITokenRefreshService, TokenRefreshService>();

    // Sprint 7: Sistema de notificaciones in-app
    builder.Services.AddScoped<INotificationService, NotificationService>();

    // Sprint 5: Configurar clientes Refit para APIs externas
    builder.Services.AddRefitClient<IXApiClient>()
        .ConfigureHttpClient(c => c.BaseAddress = new Uri("https://api.x.com"));

    builder.Services.AddRefitClient<IMetaGraphApiClient>()
        .ConfigureHttpClient(c => c.BaseAddress = new Uri("https://graph.facebook.com/v18.0"));

    builder.Services.AddRefitClient<ITikTokApiClient>()
        .ConfigureHttpClient(c => c.BaseAddress = new Uri("https://open.tiktokapis.com"));

    // Sprint 5: Servicio de YouTube con SDK oficial de Google
    builder.Services.AddScoped<YouTubeApiService>();

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

    // Servir archivos de uploads
    var uploadsPath = builder.Configuration.GetSection("Storage:UploadsPath").Value;
    if (!string.IsNullOrEmpty(uploadsPath) && Directory.Exists(uploadsPath))
    {
        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = new PhysicalFileProvider(uploadsPath),
            RequestPath = "/uploads"
        });
    }

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

    // Mapear controladores MVC (para OAuth endpoints)
    app.MapControllers();

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

    // Sprint 6: Jobs de renovación automática de tokens OAuth
    RecurringJob.AddOrUpdate<TokenRefreshJob>(
        "refrescar-tokens-oauth",
        job => job.RefreshExpiringTokensAsync(),
        "*/15 * * * *"); // Cada 15 minutos

    RecurringJob.AddOrUpdate<TokenRefreshJob>(
        "limpiar-estados-oauth",
        job => job.CleanupExpiredStatesAsync(),
        "0 * * * *"); // Cada hora

    // Sprint 7: Jobs de health check y notificaciones
    RecurringJob.AddOrUpdate<ChannelHealthCheckJob>(
        "verificar-salud-canales",
        job => job.CheckChannelHealthAsync(),
        "0 */2 * * *"); // Cada 2 horas

    RecurringJob.AddOrUpdate<ChannelHealthCheckJob>(
        "limpiar-notificaciones-expiradas",
        job => job.CleanupExpiredNotificationsAsync(),
        "0 3 * * *"); // Cada día a las 3:00 AM

    Log.Information("SocialPanelCore application started successfully");
    Log.Information("Hangfire Dashboard disponible en: /hangfire");
    Log.Information("OAuth endpoints disponibles en: /oauth/connect/{provider}");

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
