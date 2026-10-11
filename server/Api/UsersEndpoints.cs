using Afterpelago.Auth;
using Afterpelago.Data;
using Afterpelago.Domain;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Afterpelago.Api;

/// <summary>Maps admin operations for registered users and access approvals.</summary>
public static class UsersEndpoints
{
    /// <summary>Maps the admin-only Users API.</summary>
    public static RouteGroupBuilder MapUsersEndpoints(this IEndpointRouteBuilder routes)
    {
        var api = routes.MapGroup("/api/users")
            .WithTags("Users")
            .RequireAuthorization(AuthSetup.AdminOnlyPolicy);

        api.MapGet("/user-approvals", GetUserApprovals)
            .WithName("GetUserApprovals");
        api.MapPut("/user-approvals/{discordUserId}", UpdateUserApproval)
            .WithName("UpdateUserApproval")
            .Produces<UserApprovalResponse>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return api;
    }

    /// <summary>Lists all registered users, ordered by most recent access request.</summary>
    private static async Task<IReadOnlyList<UserApprovalResponse>> GetUserApprovals(
        AfterpelagoDbContext db,
        CancellationToken ct)
    {
        var users = await db.AccessRecords.AsNoTracking()
            .OrderByDescending(user => user.RequestedAt)
            .ThenBy(user => user.DisplayName)
            .ToListAsync(ct);

        return users.Select(ToResponse).ToList();
    }

    /// <summary>Changes a user's access status to Approved or Denied.</summary>
    private static async Task<IResult> UpdateUserApproval(
        string discordUserId,
        UpdateUserApprovalRequest request,
        AfterpelagoDbContext db,
        CancellationToken ct)
    {
        var status = request.Status switch
        {
            nameof(AccessStatus.Approved) => AccessStatus.Approved,
            nameof(AccessStatus.Denied) => AccessStatus.Denied,
            _ => (AccessStatus?)null,
        };
        if (status is null)
        {
            return TypedResults.Problem(
                title: "Invalid user approval status.",
                detail: "Status must be either Approved or Denied.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        var user = await db.AccessRecords.SingleOrDefaultAsync(record => record.DiscordUserId == discordUserId, ct);
        if (user is null) return TypedResults.NotFound();

        user.Status = status.Value;
        await db.SaveChangesAsync(ct);
        return TypedResults.Ok(ToResponse(user));
    }

    /// <summary>Copies a tracked access record into the public API shape.</summary>
    private static UserApprovalResponse ToResponse(AccessRecord user) =>
        new(user.DiscordUserId, user.DisplayName, user.Username, user.AvatarHash, user.Status.ToString(), user.Role, user.RequestedAt, user.LastSeenAt);
}
