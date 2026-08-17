namespace SyntaxCircus.AspNetCore.Common.MassTransit;

/// <summary>
/// Extension methods that wire the correlation-ID propagation filters into a MassTransit bus.
/// </summary>
/// <remarks>
/// <para>
/// Register once inside <c>AddMassTransit</c> and once inside the transport configurator lambda:
/// </para>
/// <code>
/// services.AddMassTransit(x =>
/// {
///     x.UseCorrelationIdPropagation();            // registers filters + consume pipeline
///
///     x.UsingRabbitMq((ctx, cfg) =>
///     {
///         cfg.UseCorrelationIdPropagation(ctx);   // wires publish + send pipeline
///         cfg.ConfigureEndpoints(ctx);
///     });
/// });
/// </code>
/// </remarks>
public static class CorrelationIdBusExtensions
{
    /// <summary>
    /// Registers the correlation-ID filter types in the DI container and configures the
    /// consume pipeline for all auto-registered receive endpoints.
    /// Call this inside the <c>AddMassTransit(x => …)</c> lambda.
    /// </summary>
    public static IBusRegistrationConfigurator UseCorrelationIdPropagation(
        this IBusRegistrationConfigurator configurator)
    {
        ArgumentNullException.ThrowIfNull(configurator);

        configurator.AddConfigureEndpointsCallback((context, _, cfg) =>
            cfg.UseConsumeFilter(typeof(CorrelationConsumeFilter<>), context));

        return configurator;
    }

    /// <summary>
    /// Wires the correlation-ID publish and send filters onto the bus factory.
    /// Call this inside the transport configurator lambda (e.g. <c>UsingRabbitMq((ctx, cfg) => …)</c>).
    /// </summary>
    public static IBusFactoryConfigurator UseCorrelationIdPropagation(
        this IBusFactoryConfigurator configurator,
        IRegistrationContext context)
    {
        ArgumentNullException.ThrowIfNull(configurator);
        ArgumentNullException.ThrowIfNull(context);

        configurator.UsePublishFilter(typeof(CorrelationPublishFilter<>), context);
        configurator.UseSendFilter(typeof(CorrelationSendFilter<>), context);

        return configurator;
    }
}
