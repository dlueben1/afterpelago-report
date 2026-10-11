using System.Reflection;
using Afterpelago.Api;
using Afterpelago.Auth;
using Afterpelago.Data;
using Afterpelago.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http.Features;

// ContentRoot is the app folder (not the working directory) so a published copy finds wwwroot/appsettings from anywhere.
var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    ContentRootPath = AppContext.BaseDirectory,
});

// The build-time OpenAPI generator boots this entry point with a mock server; keep it free of data/secret side effects.
var isContractGeneration = Assembly.GetEntryAssembly()?.GetName().Name == "GetDocument.Insider";

builder.Services.Configure<AppDataOptions>(builder.Configuration.GetSection(AppDataOptions.SectionName));
builder.Services.AddSingleton<DataPaths>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddDbContext<AfterpelagoDbContext>((sp, options) =>
    options.UseSqlite(sp.GetRequiredService<DataPaths>().ConnectionString));
builder.Services.AddScoped<SessionService>();
builder.Services.Configure<FormOptions>(options => options.MultipartBodyLengthLimit = SessionService.MaximumFileLength + 1024 * 1024);

builder.AddAfterpelagoAuth(isContractGeneration);

builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();

var app = builder.Build();

if (!isContractGeneration && !EF.IsDesignTime)
{
    app.Services.GetRequiredService<DataPaths>().EnsureCreated();
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<AfterpelagoDbContext>().Database.MigrateAsync();
    await scope.ServiceProvider.GetRequiredService<SessionService>().CleanupAsync(CancellationToken.None);
}

app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    // JSON contract only; no interactive UI (add Scalar/Swagger UI here later if wanted).
    app.MapOpenApi();
}

app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();
app.UseMiddleware<AntiforgeryValidationMiddleware>();

app.MapAuthEndpoints();
if (app.Environment.IsDevelopment() && app.Services.GetRequiredService<AuthCapabilities>().DevLogin)
{
    app.MapDevLoginEndpoint();
}

app.MapDemoEndpoints().RequireAuthorization();
app.MapAdminEndpoints().RequireAuthorization(AuthSetup.AdminOnlyPolicy);
app.MapSessionEndpoints();
app.MapUsersEndpoints();

// Unknown API routes must be real 404s, never the SPA shell.
app.Map("/api/{**rest}", () => Results.NotFound());

// Compiled Solid app, present only in published output. Missing files stay 404; only extension-less paths fall back to index.html.
if (Directory.Exists(app.Environment.WebRootPath))
{
    app.UseStaticFiles();
    app.MapFallbackToFile("/{**path:nonfile}", "index.html");
}

app.Run();

public partial class Program;
