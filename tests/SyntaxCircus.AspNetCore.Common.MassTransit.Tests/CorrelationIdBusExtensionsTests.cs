using Microsoft.Extensions.DependencyInjection;

namespace SyntaxCircus.AspNetCore.Common.MassTransit.Tests;

public sealed class CorrelationIdBusExtensionsTests
{
    [Fact]
    public void UseCorrelationIdPropagation_OnRegistrationConfigurator_DoesNotThrow()
    {
        // Verifies the registration-time extension method completes without error.
        // Full pipeline wiring is validated via integration tests with a real bus;
        // this unit test guards the registration path.
        var exception = Record.Exception(() =>
            new ServiceCollection().AddMassTransit(x =>
            {
                x.UseCorrelationIdPropagation();
                x.UsingInMemory((ctx, cfg) => cfg.ConfigureEndpoints(ctx));
            }));

        exception.ShouldBeNull();
    }

    [Fact]
    public void UseCorrelationIdPropagation_OnBusFactoryConfigurator_DoesNotThrow()
    {
        var exception = Record.Exception(() =>
            new ServiceCollection().AddMassTransit(x =>
            {
                x.UseCorrelationIdPropagation();
                x.UsingInMemory((ctx, cfg) =>
                {
                    cfg.UseCorrelationIdPropagation(ctx);
                    cfg.ConfigureEndpoints(ctx);
                });
            }));

        exception.ShouldBeNull();
    }
}
