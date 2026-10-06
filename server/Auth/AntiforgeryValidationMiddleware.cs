using Microsoft.AspNetCore.Antiforgery;

namespace Afterpelago.Auth;

/// <summary>
/// Cookie-authenticated APIs need CSRF protection: every state-changing /api request must carry the antiforgery
/// token (header X-CSRF-TOKEN) obtained from GET /api/auth/antiforgery. Done as middleware so no endpoint can forget it.
/// </summary>
public sealed class AntiforgeryValidationMiddleware(RequestDelegate next)
{
    public const string HeaderName = "X-CSRF-TOKEN";

    public async Task InvokeAsync(HttpContext context, IAntiforgery antiforgery)
    {
        if (!HttpMethods.IsGet(context.Request.Method)
            && !HttpMethods.IsHead(context.Request.Method)
            && !HttpMethods.IsOptions(context.Request.Method)
            && !HttpMethods.IsTrace(context.Request.Method)
            && context.Request.Path.StartsWithSegments("/api"))
        {
            try
            {
                await antiforgery.ValidateRequestAsync(context);
            }
            catch (AntiforgeryValidationException)
            {
                await Results.Problem(
                    title: "Invalid or missing antiforgery token.",
                    statusCode: StatusCodes.Status400BadRequest).ExecuteAsync(context);
                return;
            }
        }

        await next(context);
    }
}
