namespace SyntaxCircus.AspNetCore.Common.MassTransit;

/// <summary>
/// MassTransit consume-pipeline filter that reads the configured correlation-ID header from an
/// inbound message, writes it into <see cref="CorrelationContextAccessor"/>, and pushes
/// <c>CorrelationId</c>, <c>TraceId</c>, and <c>SpanId</c> into the logger scope for the duration
/// of the consume, matching the enrichment shape of <c>CorrelationIdMiddleware</c>.
/// </summary>
public sealed class CorrelationConsumeFilter<T>(
    IOptions<CorrelationIdOptions> options,
    ILogger<CorrelationConsumeFilter<T>> logger) : IFilter<ConsumeContext<T>>
    where T : class
{
    public async Task Send(ConsumeContext<T> context, IPipe<ConsumeContext<T>> next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        var headerName = options.Value.HeaderName;
        context.Headers.TryGetHeader(headerName, out var inbound);
        var inboundValue = inbound as string;

        var correlationId = CorrelationContextAccessor.ResolveCorrelationId(
            string.IsNullOrWhiteSpace(inboundValue) ? null : inboundValue.Trim());

        var previous = CorrelationContextAccessor.CurrentCorrelationId;
        CorrelationContextAccessor.CurrentCorrelationId = correlationId;

        var activity = Activity.Current;
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
                await next.Send(context);
            }
        }
        finally
        {
            CorrelationContextAccessor.CurrentCorrelationId = previous;
        }
    }

    public void Probe(ProbeContext context) =>
        context.CreateFilterScope("correlationConsumeFilter");
}
