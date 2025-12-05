using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Primitives;
using SocialPanelCore.Components.Account.Pages;
using SocialPanelCore.Components.Account.Pages.Manage;
using SocialPanelCore.Domain.Entities;

namespace Microsoft.AspNetCore.Routing
{
    internal static class IdentityComponentsEndpointRouteBuilderExtensions
    {
        // These endpoints are required by the Identity Razor components defined in the /Components/Account/Pages directory of this project.
        public static IEndpointConventionBuilder MapAdditionalIdentityEndpoints(this IEndpointRouteBuilder endpoints)
        {
            ArgumentNullException.ThrowIfNull(endpoints);

            var accountGroup = endpoints.MapGroup("/Account");

            accountGroup.MapPost("/PerformExternalLogin", (
                HttpContext context,
                [FromServices] SignInManager<User> signInManager,
                [FromForm] string provider,
                [FromForm] string returnUrl) =>
            {
                IEnumerable<KeyValuePair<string, StringValues>> query = [
                    new("ReturnUrl", returnUrl),
                    new("Action", ExternalLogin.LoginCallbackAction)];

                var redirectUrl = UriHelper.BuildRelative(
                    context.Request.PathBase,
                    "/Account/ExternalLogin",
                    QueryString.Create(query));

                var properties = signInManager.ConfigureExternalAuthenticationProperties(provider, redirectUrl);
                return TypedResults.Challenge(properties, [provider]);
            });

            accountGroup.MapPost("/PerformLogin", async (
                HttpContext context,
                [FromServices] SignInManager<User> signInManager,
                [FromServices] ILogger<User> logger,
                [FromForm] string? email,
                [FromForm] string? password,
                [FromForm] string? rememberMe,
                [FromForm] string? returnUrl,
                [FromForm(Name = "Input.Passkey.CredentialJson")] string? passkeyCredentialJson,
                [FromForm(Name = "Input.Passkey.Error")] string? passkeyError) =>
            {
                // Check if this is a passkey login
                if (!string.IsNullOrEmpty(passkeyCredentialJson) || !string.IsNullOrEmpty(passkeyError))
                {
                    if (!string.IsNullOrEmpty(passkeyError))
                    {
                        return TypedResults.LocalRedirect($"~/Account/Login?error=PasskeyError&returnUrl={returnUrl}");
                    }

                    var result = await signInManager.PasskeySignInAsync(passkeyCredentialJson!);

                    if (result.Succeeded)
                    {
                        logger.LogInformation("User logged in with passkey.");
                        return TypedResults.LocalRedirect($"~/{returnUrl ?? ""}");
                    }
                    else if (result.RequiresTwoFactor)
                    {
                        return TypedResults.LocalRedirect($"~/Account/LoginWith2fa?returnUrl={returnUrl}");
                    }
                    else if (result.IsLockedOut)
                    {
                        logger.LogWarning("User account locked out.");
                        return TypedResults.LocalRedirect("~/Account/Lockout");
                    }
                    else
                    {
                        return TypedResults.LocalRedirect($"~/Account/Login?error=InvalidCredentials&returnUrl={returnUrl}");
                    }
                }

                // Password-based login
                if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
                {
                    return TypedResults.LocalRedirect($"~/Account/Login?error=InvalidCredentials&returnUrl={returnUrl}");
                }

                // HTML checkbox sends "on" when checked, null when unchecked
                bool rememberMeBool = rememberMe == "on" || rememberMe == "true";
                var passwordResult = await signInManager.PasswordSignInAsync(email, password, rememberMeBool, lockoutOnFailure: false);

                if (passwordResult.Succeeded)
                {
                    logger.LogInformation("User {Email} logged in.", email);
                    return TypedResults.LocalRedirect($"~/{returnUrl ?? ""}");
                }
                else if (passwordResult.RequiresTwoFactor)
                {
                    return TypedResults.LocalRedirect($"~/Account/LoginWith2fa?returnUrl={returnUrl}&rememberMe={rememberMeBool}");
                }
                else if (passwordResult.IsLockedOut)
                {
                    logger.LogWarning("User {Email} account locked out.", email);
                    return TypedResults.LocalRedirect("~/Account/Lockout");
                }
                else
                {
                    // Redirect back to login with error
                    return TypedResults.LocalRedirect($"~/Account/Login?error=InvalidCredentials&returnUrl={returnUrl}");
                }
            });

            accountGroup.MapPost("/Logout", async (
                ClaimsPrincipal user,
                [FromServices] SignInManager<User> signInManager,
                [FromForm] string returnUrl) =>
            {
                await signInManager.SignOutAsync();
                return TypedResults.LocalRedirect($"~/{returnUrl}");
            });

            accountGroup.MapPost("/PasskeyCreationOptions", async (
                HttpContext context,
                [FromServices] UserManager<User> userManager,
                [FromServices] SignInManager<User> signInManager,
                [FromServices] IAntiforgery antiforgery) =>
            {
                await antiforgery.ValidateRequestAsync(context);

                var user = await userManager.GetUserAsync(context.User);
                if (user is null)
                {
                    return Results.NotFound($"Unable to load user with ID '{userManager.GetUserId(context.User)}'.");
                }

                var userId = await userManager.GetUserIdAsync(user);
                var userName = await userManager.GetUserNameAsync(user) ?? "User";
                var optionsJson = await signInManager.MakePasskeyCreationOptionsAsync(new()
                {
                    Id = userId,
                    Name = userName,
                    DisplayName = userName
                });
                return TypedResults.Content(optionsJson, contentType: "application/json");
            });

            accountGroup.MapPost("/PasskeyRequestOptions", async (
                HttpContext context,
                [FromServices] UserManager<User> userManager,
                [FromServices] SignInManager<User> signInManager,
                [FromServices] IAntiforgery antiforgery,
                [FromQuery] string? username) =>
            {
                await antiforgery.ValidateRequestAsync(context);

                var user = string.IsNullOrEmpty(username) ? null : await userManager.FindByNameAsync(username);
                var optionsJson = await signInManager.MakePasskeyRequestOptionsAsync(user);
                return TypedResults.Content(optionsJson, contentType: "application/json");
            });

            var manageGroup = accountGroup.MapGroup("/Manage").RequireAuthorization();

            manageGroup.MapPost("/LinkExternalLogin", async (
                HttpContext context,
                [FromServices] SignInManager<User> signInManager,
                [FromForm] string provider) =>
            {
                // Clear the existing external cookie to ensure a clean login process
                await context.SignOutAsync(IdentityConstants.ExternalScheme);

                var redirectUrl = UriHelper.BuildRelative(
                    context.Request.PathBase,
                    "/Account/Manage/ExternalLogins",
                    QueryString.Create("Action", ExternalLogins.LinkLoginCallbackAction));

                var properties = signInManager.ConfigureExternalAuthenticationProperties(provider, redirectUrl, signInManager.UserManager.GetUserId(context.User));
                return TypedResults.Challenge(properties, [provider]);
            });

            var loggerFactory = endpoints.ServiceProvider.GetRequiredService<ILoggerFactory>();
            var downloadLogger = loggerFactory.CreateLogger("DownloadPersonalData");

            manageGroup.MapPost("/DownloadPersonalData", async (
                HttpContext context,
                [FromServices] UserManager<User> userManager,
                [FromServices] AuthenticationStateProvider authenticationStateProvider) =>
            {
                var user = await userManager.GetUserAsync(context.User);
                if (user is null)
                {
                    return Results.NotFound($"Unable to load user with ID '{userManager.GetUserId(context.User)}'.");
                }

                var userId = await userManager.GetUserIdAsync(user);
                downloadLogger.LogInformation("User with ID '{UserId}' asked for their personal data.", userId);

                // Only include personal data for download
                var personalData = new Dictionary<string, string>();
                var personalDataProps = typeof(User).GetProperties().Where(
                    prop => Attribute.IsDefined(prop, typeof(PersonalDataAttribute)));
                foreach (var p in personalDataProps)
                {
                    personalData.Add(p.Name, p.GetValue(user)?.ToString() ?? "null");
                }

                var logins = await userManager.GetLoginsAsync(user);
                foreach (var l in logins)
                {
                    personalData.Add($"{l.LoginProvider} external login provider key", l.ProviderKey);
                }

                personalData.Add("Authenticator Key", (await userManager.GetAuthenticatorKeyAsync(user))!);
                var fileBytes = JsonSerializer.SerializeToUtf8Bytes(personalData);

                context.Response.Headers.TryAdd("Content-Disposition", "attachment; filename=PersonalData.json");
                return TypedResults.File(fileBytes, contentType: "application/json", fileDownloadName: "PersonalData.json");
            });

            return accountGroup;
        }
    }
}
