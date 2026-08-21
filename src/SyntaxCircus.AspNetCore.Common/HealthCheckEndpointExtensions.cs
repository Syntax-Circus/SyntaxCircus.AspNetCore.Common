using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace SyntaxCircus.AspNetCore.Common;

public static class HealthCheckEndpointExtensions
{
    /// <summary>
    /// Maps a liveness endpoint at <paramref name="livePath"/> (no checks run — just confirms the process
    /// is up) and a readiness endpoint at <paramref name="readyPath"/> (runs checks tagged
    /// <paramref name="readyTag"/>), both rendered via <see cref="HealthCheckResponseWriter"/>.
    /// <paramref name="metadataFactoryAsync"/> and <paramref name="metadataFactory"/> are optional and, when
    /// given, are invoked per-request and their result included under the response's <c>metadata</c> key
    /// (e.g. to surface an app version, or async state like a background job snapshot) — see
    /// <see cref="HealthCheckResponseWriter.WriteJsonAsync"/>. When both are given,
    /// <paramref name="metadataFactoryAsync"/> takes precedence.
    /// </summary>
    public static IEndpointRouteBuilder MapStandardHealthChecks(
        this IEndpointRouteBuilder endpoints,
        string readyTag = "ready",
        Func<HttpContext, IReadOnlyDictionary<string, object?>>? metadataFactory = null,
        string livePath = "/health/live",
        string readyPath = "/health/ready",
        Func<HttpContext, Task<IReadOnlyDictionary<string, object?>>>? metadataFactoryAsync = null)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        async Task WriteResponse(HttpContext context, HealthReport report)
        {
            var metadata = metadataFactoryAsync is not null
                ? await metadataFactoryAsync(context)
                : metadataFactory?.Invoke(context);

            await HealthCheckResponseWriter.WriteJsonAsync(context, report, metadata);
        }

        endpoints.MapHealthChecks(livePath, new HealthCheckOptions
        {
            Predicate = _ => false,
            ResponseWriter = WriteResponse,
        });

        endpoints.MapHealthChecks(readyPath, new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains(readyTag),
            ResponseWriter = WriteResponse,
        });

        return endpoints;
    }
}
