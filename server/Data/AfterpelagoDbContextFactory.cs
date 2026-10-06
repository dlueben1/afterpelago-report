using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Options;

namespace Afterpelago.Data;

/// <summary>Lets `dotnet ef` work without booting the web host (and without Discord/production settings).</summary>
public sealed class AfterpelagoDbContextFactory : IDesignTimeDbContextFactory<AfterpelagoDbContext>
{
    public AfterpelagoDbContext CreateDbContext(string[] args)
    {
        var environmentName = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? Environments.Development;

        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile($"appsettings.{environmentName}.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var options = new AppDataOptions();
        configuration.GetSection(AppDataOptions.SectionName).Bind(options);

        var paths = new DataPaths(Options.Create(options), new DesignTimeEnvironment(environmentName));
        paths.EnsureCreated();

        var builder = new DbContextOptionsBuilder<AfterpelagoDbContext>().UseSqlite(paths.ConnectionString);
        return new AfterpelagoDbContext(builder.Options);
    }

    private sealed class DesignTimeEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;
        public string ApplicationName { get; set; } = "Afterpelago";
        public string ContentRootPath { get; set; } = Directory.GetCurrentDirectory();
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = new Microsoft.Extensions.FileProviders.NullFileProvider();
    }
}
