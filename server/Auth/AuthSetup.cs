using System.Security.Claims;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using Afterpelago.Data;
using AspNet.Security.OAuth.Discord;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;

namespace Afterpelago.Auth;

public static class AuthSetup
{
    public static void AddAfterpelagoAuth(this WebApplicationBuilder builder, bool isContractGeneration = false)
    {
        var services = builder.Services;
        var configuration = builder.Configuration;

        var devLogin = new DevLoginOptions();
        configuration.GetSection(DevLoginOptions.SectionName).Bind(devLogin);
        if (devLogin.Enabled && !builder.Environment.IsDevelopment())
        {
            // Fail closed: the test identity can never be switched on outside Development.
            throw new InvalidOperationException(
                $"'{DevLoginOptions.SectionName}:Enabled' may only be true when the environment is Development (current: '{builder.Environment.EnvironmentName}').");
        }

        // Discord is only registered when credentials exist; the OAuth handler throws on every request otherwise.
        var discordSection = configuration.GetSection("Authentication:Discord");
        var discordConfigured = !string.IsNullOrWhiteSpace(discordSection["ClientId"]) && !string.IsNullOrWhiteSpace(discordSection["ClientSecret"]);
        services.AddSingleton(new AuthCapabilities(discordConfigured, devLogin.Enabled));
        services.AddSingleton(devLogin);

        services.AddScoped<AccessCookieEvents>();

        var authentication = services.AddAuthentication(AuthSchemes.Application);

        authentication.AddCookie(AuthSchemes.Application, options =>
        {
            options.Cookie.Name = "afterpelago.session";
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Lax;
            options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
            options.ExpireTimeSpan = TimeSpan.FromHours(8);
            options.SlidingExpiration = true;
            options.EventsType = typeof(AccessCookieEvents);
        });

        authentication.AddCookie(AuthSchemes.External, options =>
        {
            options.Cookie.Name = "afterpelago.external";
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Lax;
            options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
            options.ExpireTimeSpan = TimeSpan.FromMinutes(5);
        });

        if (discordConfigured)
        {
            authentication.AddDiscord(AuthSchemes.Discord, options =>
            {
                discordSection.Bind(options);

                options.SignInScheme = AuthSchemes.External;
                options.CallbackPath = "/api/auth/discord-callback";
                options.SaveTokens = false;
                options.UsePkce = true;

                // Authorization-code callback is a top-level GET, so Lax works; the default (None+Secure) breaks plain-HTTP localhost.
                options.CorrelationCookie.SameSite = SameSiteMode.Lax;
                options.CorrelationCookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;

                // Identity only: this is enforced after configuration binding so config cannot widen it.
                options.Scope.Clear();
                options.Scope.Add("identify");

                options.Events.OnCreatingTicket = context =>
                {
                    // Copy the stable fields we use out of Discord's /users/@me response.
                    AddClaim(context, context.User, "id", AuthClaims.DiscordId);
                    AddClaim(context, context.User, "username", AuthClaims.DiscordUsername);
                    AddClaim(context, context.User, "global_name", AuthClaims.DiscordGlobalName);
                    return Task.CompletedTask;
                };

                options.Events.OnRemoteFailure = context =>
                {
                    // e.g. the user pressed "Cancel" at Discord, or state/correlation validation failed.
                    context.HttpContext.RequestServices.GetRequiredService<ILoggerFactory>()
                        .CreateLogger("Afterpelago.Auth")
                        .LogWarning(context.Failure, "Discord sign-in failed.");
                    context.Response.Redirect("/login?signin=failed");
                    context.HandleResponse();
                    return Task.CompletedTask;
                };
            });
        }

        services.AddAuthorization();

        services.AddAntiforgery(options =>
        {
            options.HeaderName = AntiforgeryValidationMiddleware.HeaderName;
            options.Cookie.Name = "afterpelago.csrf";
            options.Cookie.SameSite = SameSiteMode.Strict;
            options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        });

        AddDataProtection(builder, isContractGeneration);
    }

    private static void AddDataProtection(WebApplicationBuilder builder, bool isContractGeneration)
    {
        var dataProtection = builder.Services.AddDataProtection().SetApplicationName("Afterpelago");

        if (isContractGeneration)
        {
            // Build-time OpenAPI generation must not need or touch the data directory.
            dataProtection.UseEphemeralDataProtectionProvider();
            return;
        }

        // Keys live under the data directory so cookies survive restarts and redeploys.
        builder.Services.AddOptions<KeyManagementOptions>().Configure<DataPaths, ILoggerFactory>((options, paths, loggerFactory) =>
        {
            paths.EnsureCreated();
            options.XmlRepository = new Microsoft.AspNetCore.DataProtection.Repositories.FileSystemXmlRepository(new DirectoryInfo(paths.KeysDirectory), loggerFactory);
        });

        // Explicit key persistence disables automatic at-rest encryption. Optionally encrypt keys with a portable PFX.
        var certificatePath = builder.Configuration["Afterpelago:DataProtection:KeyCertificatePath"];
        if (!string.IsNullOrWhiteSpace(certificatePath))
        {
            var password = builder.Configuration["Afterpelago:DataProtection:KeyCertificatePassword"];
            dataProtection.ProtectKeysWithCertificate(X509CertificateLoader.LoadPkcs12FromFile(certificatePath, password));
        }
    }

    private static void AddClaim(OAuthCreatingTicketContext context, JsonElement user, string property, string claimType)
    {
        if (user.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String && value.GetString() is { Length: > 0 } text)
        {
            context.Identity?.AddClaim(new Claim(claimType, text));
        }
    }
}
