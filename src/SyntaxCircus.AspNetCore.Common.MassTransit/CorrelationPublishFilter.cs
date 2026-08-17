namespace SyntaxCircus.AspNetCore.Common.MassTransit;

/// <summary>
/// MassTransit publish-pipeline filter that forwards the current correlation ID (from
/// <see cref="CorrelationContextAccessor"/>) as a header on every outbound published message,
/// using the configured header name from <see cref="CorrelationIdOptions"/>.
/// </summary>
public sealed class CorrelationPublishFilter<T>(IOptions<CorrelationIdOptions> options)
    : IFilter<PublishContext<T>>
    where T : class
{
    public Task Send(PublishContext<T> context, IPipe<PublishContext<T>> next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        var correlationId = CorrelationContextAccessor.ResolveCorrelationId();
        context.Headers.Set(options.Value.HeaderName, correlationId);

        return next.Send(context);
    }

    public void Probe(ProbeContext context) =>
        context.CreateFilterScope("correlationPublishFilter");
}
