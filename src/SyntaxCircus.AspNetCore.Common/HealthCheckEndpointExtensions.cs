using Microsoft.AspNetCore.Diagnostics.HealthChecks;

namespace SyntaxCircus.AspNetCore.Common;

public static class HealthCheckEndpointExtensions
{
    /// <summary>
    /// Maps <c>/health/live</c> (no checks run — just confirms the process is up) and
    /// <c>/health/ready</c> (runs checks tagged <paramref name="readyTag"/>), both rendered via
    /// <see cref="HealthCheckResponseWriter"/>.
    /// </summary>
    public static IEndpointRouteBuilder MapStandardHealthChecks(this IEndpointRouteBuilder endpoints, string readyTag = "ready")
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints.MapHealthChecks("/health/live", new HealthCheckOptions
        {
            Predicate = _ => false,
            ResponseWriter = HealthCheckResponseWriter.WriteJsonAsync,
        });

        endpoints.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains(readyTag),
            ResponseWriter = HealthCheckResponseWriter.WriteJsonAsync,
        });

        return endpoints;
    }
}
