namespace SyntaxCircus.AspNetCore.Common.Tests;

public class CorrelationIdExtensionsTests
{
    [Fact]
    public void AddCorrelationId_NullServices_ThrowsArgumentNullException()
    {
        Should.Throw<ArgumentNullException>(() => CorrelationIdExtensions.AddCorrelationId(null!));
    }

    [Fact]
    public void AddCorrelationId_NoConfigureCallback_UsesDefaults()
    {
        var services = new ServiceCollection();
        services.AddCorrelationId();

        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<IOptions<CorrelationIdOptions>>().Value.HeaderName.ShouldBe("X-Correlation-Id");
    }

    [Fact]
    public void AddCorrelationId_ConfigureCallback_Applied()
    {
        var services = new ServiceCollection();
        services.AddCorrelationId(options => options.HeaderName = "X-Custom-Id");

        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<IOptions<CorrelationIdOptions>>().Value.HeaderName.ShouldBe("X-Custom-Id");
    }

    [Fact]
    public void UseCorrelationId_NullApp_ThrowsArgumentNullException()
    {
        Should.Throw<ArgumentNullException>(() => CorrelationIdExtensions.UseCorrelationId(null!));
    }

    [Fact]
    public void UseCorrelationId_ReturnsSameApplicationBuilder()
    {
        var services = new ServiceCollection();
        services.AddCorrelationId();
        services.AddLogging();
        var app = new ApplicationBuilder(services.BuildServiceProvider());

        var result = app.UseCorrelationId();

        result.ShouldBeSameAs(app);
    }
}
