using Microsoft.Extensions.Options;

namespace Afterpelago.Data;

/// <summary>
/// Resolves every mutable path under one data directory. Production must configure an absolute directory;
/// Development may use a relative one, resolved against the current working directory.
/// </summary>
public sealed class DataPaths
{
    public DataPaths(IOptions<AppDataOptions> options, IHostEnvironment environment)
    {
        var configured = options.Value.DataDirectory;

        if (configured.Length == 0)
        {
            throw new InvalidOperationException(
                $"'{AppDataOptions.SectionName}:{nameof(AppDataOptions.DataDirectory)}' is not configured. " +
                $"Set it in configuration or via the {AppDataOptions.SectionName}__{nameof(AppDataOptions.DataDirectory)} environment variable.");
        }

        if (!environment.IsDevelopment() && !Path.IsPathFullyQualified(configured))
        {
            throw new InvalidOperationException(
                $"'{AppDataOptions.SectionName}:{nameof(AppDataOptions.DataDirectory)}' must be an absolute path outside Development (got '{configured}').");
        }

        Directory = Path.GetFullPath(configured);
    }

    public string Directory { get; }

    public string DatabasePath => Path.Combine(Directory, "afterpelago.db");

    public string KeysDirectory => Path.Combine(Directory, "keys");

    public string ConnectionString => $"Data Source={DatabasePath}";

    public void EnsureCreated()
    {
        System.IO.Directory.CreateDirectory(Directory);
        System.IO.Directory.CreateDirectory(KeysDirectory);
    }
}
