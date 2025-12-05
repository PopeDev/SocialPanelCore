using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MudBlazor.Services;
using Serilog;
using SocialPanelCore.Infrastructure.BackgroundServices;
using SocialPanelCore.Application.Integration;
using SocialPanelCore.Infrastructure.Services;
using SocialPanelCore.Components;
using SocialPanelCore.Components.Account;
using SocialPanelCore.Domain.Interfaces;
using SocialPanelCore.Infrastructure.Data;
using SocialPanelCore.Domain.Entities;

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

    // Servicios de Infrastructure
    builder.Services.AddScoped<TokenProtectionService>();
    builder.Services.AddScoped<IMediaStorageService, MediaStorageService>();

    // Servicios de Application
    builder.Services.AddScoped<IUserService, UserService>();
    builder.Services.AddScoped<IAccountService, AccountService>();
    builder.Services.AddScoped<ISocialChannelConfigService, SocialChannelConfigService>();
    builder.Services.AddScoped<IBasePostService, BasePostService>();
    builder.Services.AddScoped<IContentAdaptationService, ContentAdaptationService>();
    builder.Services.AddScoped<ISocialPublisherService, SocialPublisherService>();

    // OpenRouter Configuration y Client
    builder.Services.Configure<OpenRouterOptions>(
        builder.Configuration.GetSection(OpenRouterOptions.SectionName));

    builder.Services.AddHttpClient<OpenRouterClient>()
        .ConfigureHttpClient(client => client.Timeout = TimeSpan.FromSeconds(60));

    // Hosted Services para procesos en background
    builder.Services.AddHostedService<AdaptationHostedService>();
    builder.Services.AddHostedService<PublishingHostedService>();

    var app = builder.Build();

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

    app.MapStaticAssets();
    app.MapRazorComponents<App>()
        .AddInteractiveServerRenderMode();

    // Add additional endpoints required by the Identity /Account Razor components.
    app.MapAdditionalIdentityEndpoints();

    // Crear carpeta de logs si no existe
    var logsPath = Path.Combine(Directory.GetCurrentDirectory(), "logs");
    if (!Directory.Exists(logsPath))
        Directory.CreateDirectory(logsPath);

    // Ejecutar seeder de base de datos solo en desarrollo
    if (app.Environment.IsDevelopment())
    {
        using (var scope = app.Services.CreateScope())
        {
            try
            {
                await DatabaseSeeder.SeedAsync(scope.ServiceProvider);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error durante el seed de la base de datos");
            }
        }
    }

    Log.Information("SocialPanelCore application started successfully");

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
