namespace SyntaxCircus.AspNetCore.Common.Tests;

public class CorrelationIdMiddlewareTests : IDisposable
{
    public void Dispose()
    {
        CorrelationContextAccessor.CurrentCorrelationId = null;
        GC.SuppressFinalize(this);
    }

    private static DefaultHttpContext CreateContext()
    {
        var context = new DefaultHttpContext();
        var services = new ServiceCollection();
        services.AddLogging();
        context.RequestServices = services.BuildServiceProvider();
        return context;
    }

    private static CorrelationIdMiddleware CreateMiddleware(RequestDelegate? next = null, ILogger<CorrelationIdMiddleware>? logger = null)
        => new(next ?? (_ => Task.CompletedTask), Options.Create(new CorrelationIdOptions()), logger ?? Substitute.For<ILogger<CorrelationIdMiddleware>>());

    [Fact]
    public async Task Invoke_InboundHeaderPresent_UsedAsCorrelationId()
    {
        var context = CreateContext();
        context.Request.Headers["X-Correlation-Id"] = " inbound-id ";
        var middleware = CreateMiddleware();

        await middleware.Invoke(context);

        context.Response.Headers["X-Correlation-Id"].ToString().ShouldBe("inbound-id");
    }

    [Fact]
    public async Task Invoke_NoInboundHeader_GeneratesCorrelationIdFromTraceIdentifier()
    {
        var context = CreateContext();
        context.TraceIdentifier = "trace-abc";
        var middleware = CreateMiddleware();

        await middleware.Invoke(context);

        context.Response.Headers["X-Correlation-Id"].ToString().ShouldBe("trace-abc");
    }

    [Fact]
    public async Task Invoke_SetsResponseHeaderAndItems()
    {
        var context = CreateContext();
        context.Request.Headers["X-Correlation-Id"] = "my-id";
        var middleware = CreateMiddleware();

        await middleware.Invoke(context);

        context.Response.Headers["X-Correlation-Id"].ToString().ShouldBe("my-id");
        context.Items["X-Correlation-Id"].ShouldBe("my-id");
    }

    [Fact]
    public async Task Invoke_CustomHeaderName_Used()
    {
        var context = CreateContext();
        context.Request.Headers["X-Request-Id"] = "custom-id";
        var middleware = new CorrelationIdMiddleware(
            _ => Task.CompletedTask,
            Options.Create(new CorrelationIdOptions { HeaderName = "X-Request-Id" }),
            Substitute.For<ILogger<CorrelationIdMiddleware>>());

        await middleware.Invoke(context);

        context.Response.Headers["X-Request-Id"].ToString().ShouldBe("custom-id");
    }

    [Fact]
    public async Task Invoke_RestoresPreviousCorrelationIdAfterCompletion()
    {
        CorrelationContextAccessor.CurrentCorrelationId = "outer-id";
        var context = CreateContext();
        context.Request.Headers["X-Correlation-Id"] = "inner-id";
        var middleware = CreateMiddleware();

        await middleware.Invoke(context);

        CorrelationContextAccessor.CurrentCorrelationId.ShouldBe("outer-id");
    }

    [Fact]
    public async Task Invoke_RestoresPreviousCorrelationIdEvenWhenNextThrows()
    {
        CorrelationContextAccessor.CurrentCorrelationId = "outer-id";
        var context = CreateContext();
        var middleware = CreateMiddleware(_ => throw new InvalidOperationException("boom"));

        await Should.ThrowAsync<InvalidOperationException>(() => middleware.Invoke(context));

        CorrelationContextAccessor.CurrentCorrelationId.ShouldBe("outer-id");
    }

    [Fact]
    public async Task Invoke_CallsNextDelegate()
    {
        var context = CreateContext();
        var nextCalled = false;
        var middleware = CreateMiddleware(_ => { nextCalled = true; return Task.CompletedTask; });

        await middleware.Invoke(context);

        nextCalled.ShouldBeTrue();
    }
}
