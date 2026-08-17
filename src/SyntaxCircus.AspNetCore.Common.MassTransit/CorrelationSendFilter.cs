namespace SyntaxCircus.AspNetCore.Common.MassTransit;

/// <summary>
/// MassTransit send-pipeline filter that forwards the current correlation ID (from
/// <see cref="CorrelationContextAccessor"/>) as a header on every outbound sent message,
/// using the configured header name from <see cref="CorrelationIdOptions"/>.
/// </summary>
public sealed class CorrelationSendFilter<T>(IOptions<CorrelationIdOptions> options)
    : IFilter<SendContext<T>>
    where T : class
{
    public Task Send(SendContext<T> context, IPipe<SendContext<T>> next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        var correlationId = CorrelationContextAccessor.ResolveCorrelationId();
        context.Headers.Set(options.Value.HeaderName, correlationId);

        return next.Send(context);
    }

    public void Probe(ProbeContext context) =>
        context.CreateFilterScope("correlationSendFilter");
}
