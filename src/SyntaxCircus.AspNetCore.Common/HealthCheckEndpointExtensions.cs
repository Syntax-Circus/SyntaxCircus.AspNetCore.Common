using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace SyntaxCircus.AspNetCore.Common;

public static class HealthCheckEndpointExtensions
{
    /// <summary>
    /// Maps <c>/health/live</c> (no checks run — just confirms the process is up) and
    /// <c>/health/ready</c> (runs checks tagged <paramref name="readyTag"/>), both rendered via
    /// <see cref="HealthCheckResponseWriter"/>. <paramref name="metadataFactory"/> is optional and, when
    /// given, is invoked per-request and its result included under the response's <c>metadata</c> key
    /// (e.g. to surface an app version) — see <see cref="HealthCheckResponseWriter.WriteJsonAsync"/>.
    /// </summary>
    public static IEndpointRouteBuilder MapStandardHealthChecks(
        this IEndpointRouteBuilder endpoints,
        string readyTag = "ready",
        Func<HttpContext, IReadOnlyDictionary<string, object?>>? metadataFactory = null)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        Task WriteResponse(HttpContext context, HealthReport report) =>
            HealthCheckResponseWriter.WriteJsonAsync(context, report, metadataFactory?.Invoke(context));

        endpoints.MapHealthChecks("/health/live", new HealthCheckOptions
        {
            Predicate = _ => false,
            ResponseWriter = WriteResponse,
        });

        endpoints.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains(readyTag),
            ResponseWriter = WriteResponse,
        });

        return endpoints;
    }
}
