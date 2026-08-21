using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace SyntaxCircus.AspNetCore.Common;

public static class HealthDashboardExtensions
{
    /// <summary>
    /// Maps a self-contained HTML operator dashboard at <paramref name="pattern"/> (no external assets,
    /// always HTTP 200, <c>Cache-Control: no-store</c>) rendering the current health report — filtered to
    /// checks tagged <paramref name="readyTag"/> — plus any caller-supplied notices/sections/status groups
    /// from <paramref name="configure"/>. <paramref name="configure"/> runs per-request (not once at mapping
    /// time), so it can pull in state that changes between requests, e.g. a background job's latest snapshot.
    /// </summary>
    public static IEndpointRouteBuilder MapHealthCheckDashboard(
        this IEndpointRouteBuilder endpoints,
        string pattern = "/health/dashboard",
        string readyTag = "ready",
        Func<HttpContext, HealthDashboardOptions, Task>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints.MapGet(pattern, async (HealthCheckService hcs, HttpContext ctx) =>
        {
            var options = new HealthDashboardOptions();
            if (configure is not null)
            {
                await configure(ctx, options);
            }

            var report = await hcs.CheckHealthAsync(check => check.Tags.Contains(readyTag), ctx.RequestAborted);
            await HealthDashboardWriter.WriteHtmlAsync(ctx, report, options);
        });

        return endpoints;
    }
}
