using Afterpelago.Auth;
using Afterpelago.Data;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Afterpelago.Api;

public static class AdminEndpoints
{
    /// <summary>
    /// Mapping of Minimal API endpoints for admin operations
    /// </summary>
    public static RouteGroupBuilder MapAdminEndpoints(this IEndpointRouteBuilder routes)
    {
        var api = routes
            .MapGroup("/api/admin")
            .WithTags("Admin")
            .RequireAuthorization(AuthSetup.AdminOnlyPolicy);

        // Database Info
        api.MapGet("/db-info", GetDatabaseInfo).WithName("GetDatabaseInfo");

        return api;
    }

    /// <summary>
    /// Returns information about the database, including its path and size in gigabytes.
    /// </summary>
    private static Ok<DatabaseInfoResponse> GetDatabaseInfo(DataPaths paths)
    {
        // Get the database size
        var bytes = new FileInfo(paths.DatabasePath).Length;
        var gb = Math.Round((double)bytes / (1024 * 1024 * 1024), 3);

        return TypedResults.Ok(new DatabaseInfoResponse(paths.DatabasePath, gb));
    }
}
