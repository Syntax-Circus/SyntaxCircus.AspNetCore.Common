using System.Diagnostics;

namespace SyntaxCircus.AspNetCore.Common;

/// <summary>
/// Reads (or generates) a correlation ID for the request, echoes it on the response, tags the
/// current <see cref="Activity"/> with it, and pushes it — along with the current trace/span ID —
/// into the logger scope for the duration of the request, so every log line written downstream
/// carries it. Framework-native (<see cref="ILogger.BeginScope{TState}"/>) rather than tied to a
/// specific logging provider — Serilog and other providers that respect logger scope will surface
/// these as enriched properties automatically.
/// </summary>
public sealed class CorrelationIdMiddleware(
    RequestDelegate next,
    IOptions<CorrelationIdOptions> options,
    ILogger<CorrelationIdMiddleware> logger)
{
    public async Task Invoke(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var headerName = options.Value.HeaderName;
        var inboundCorrelationId = context.Request.Headers[headerName].FirstOrDefault();
        var correlationId = string.IsNullOrWhiteSpace(inboundCorrelationId)
            ? CorrelationContextAccessor.ResolveCorrelationId(context.TraceIdentifier)
            : inboundCorrelationId.Trim();

        var previousCorrelationId = CorrelationContextAccessor.CurrentCorrelationId;
        CorrelationContextAccessor.CurrentCorrelationId = correlationId;

        context.Response.Headers[headerName] = correlationId;
        context.Items[headerName] = correlationId;

        var activity = Activity.Current;
        activity?.SetTag("correlation_id", correlationId);
        activity?.AddBaggage("correlation_id", correlationId);

        var scopeState = new Dictionary<string, object?>
        {
            ["CorrelationId"] = correlationId,
            ["TraceId"] = activity?.TraceId.ToString(),
            ["SpanId"] = activity?.SpanId.ToString(),
        };

        try
        {
            using (logger.BeginScope(scopeState))
            {
                await next(context);
            }
        }
        finally
        {
            CorrelationContextAccessor.CurrentCorrelationId = previousCorrelationId;
        }
    }
}
