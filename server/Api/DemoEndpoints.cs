using Afterpelago.Data;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Afterpelago.Api;

public static class DemoEndpoints
{
    public static RouteGroupBuilder MapDemoEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/demo").WithTags("Demo");

        group.MapGet("/server-data", () =>
        {
            var samples = Enumerable.Range(1, 8)
                .Select(i => new SamplePoint($"Sample {i}", Math.Round(Random.Shared.NextDouble() * 100, 1)))
                .ToList();

            return TypedResults.Ok(new ServerDataResponse(
                "Hello from Afterpelago",
                DateTimeOffset.UtcNow,
                Math.Round(Random.Shared.NextDouble(), 4),
                samples));
        }).WithName("GetServerData");

        group.MapGet("/counter", async Task<Ok<CounterResponse>> (AfterpelagoDbContext db, CancellationToken ct) =>
            TypedResults.Ok(await ReadCounterAsync(db, ct))
        ).WithName("GetCounter");

        group.MapPost("/counter/increment", async Task<Ok<CounterResponse>> (AfterpelagoDbContext db, CancellationToken ct) =>
        {
            var now = DateTimeOffset.UtcNow;

            await db.DemoCounters
                .Where(c => c.Id == AfterpelagoDbContext.DemoCounterId)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(c => c.Value, c => c.Value + 1)
                    .SetProperty(c => c.UpdatedAt, now), ct);

            return TypedResults.Ok(await ReadCounterAsync(db, ct));
        }).WithName("IncrementCounter");

        return group;
    }

    private static async Task<CounterResponse> ReadCounterAsync(AfterpelagoDbContext db, CancellationToken ct) =>
        await db.DemoCounters
            .AsNoTracking()
            .Where(c => c.Id == AfterpelagoDbContext.DemoCounterId)
            .Select(c => new CounterResponse(c.Value, c.UpdatedAt))
            .SingleAsync(ct);
}
