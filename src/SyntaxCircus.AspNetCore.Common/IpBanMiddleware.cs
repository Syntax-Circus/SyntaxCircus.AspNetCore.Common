namespace SyntaxCircus.AspNetCore.Common;

/// <summary>
/// Short-circuits requests from IPs <see cref="IpBanTracker"/> has banned with a bare 403 — before rate
/// limiting, authentication, or any endpoint logic runs. Deliberately does not log per rejected request:
/// once an IP is banned, the point is for it to go quiet, not to keep producing the same log volume as
/// before the ban. Register with <see cref="IpBanExtensions.UseIpBanTracking"/>.
/// </summary>
public sealed class IpBanMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, IpBanTracker tracker, TimeProvider clock)
    {
        if (tracker.IsBanned(context.Connection.RemoteIpAddress, clock.GetUtcNow()))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }
        await next(context);
    }
}
