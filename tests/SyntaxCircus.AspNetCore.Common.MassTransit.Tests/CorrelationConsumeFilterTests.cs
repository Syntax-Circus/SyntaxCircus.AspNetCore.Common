namespace SyntaxCircus.AspNetCore.Common.MassTransit.Tests;

public sealed class CorrelationConsumeFilterTests : IDisposable
{
    public void Dispose()
    {
        CorrelationContextAccessor.CurrentCorrelationId = null;
    }

    private static CorrelationConsumeFilter<TestMessage> CreateFilter(
        string headerName = "X-Correlation-Id",
        ILogger<CorrelationConsumeFilter<TestMessage>>? logger = null)
    {
        var options = Options.Create(new CorrelationIdOptions { HeaderName = headerName });
        logger ??= Substitute.For<ILogger<CorrelationConsumeFilter<TestMessage>>>();
        return new CorrelationConsumeFilter<TestMessage>(options, logger);
    }

    private static ConsumeContext<TestMessage> CreateContext(string? headerValue = null, string headerName = "X-Correlation-Id")
    {
        var context = Substitute.For<ConsumeContext<TestMessage>>();
        object? boxed = headerValue;
        context.Headers.TryGetHeader(headerName, out Arg.Any<object?>())
            .Returns(x =>
            {
                x[1] = boxed;
                return headerValue is not null;
            });
        return context;
    }

    [Fact]
    public async Task Send_InboundHeaderPresent_SetsCurrentCorrelationId()
    {
        var filter = CreateFilter();
        var context = CreateContext("inbound-id");
        var next = Substitute.For<IPipe<ConsumeContext<TestMessage>>>();

        await filter.Send(context, next);

        // next was called (we just check no exception and next was invoked)
        await next.Received(1).Send(context);
    }

    [Fact]
    public async Task Send_InboundHeaderPresent_PropagatesHeaderValueToAccessor()
    {
        var filter = CreateFilter();
        var context = CreateContext("inbound-id");
        string? capturedId = null;
        var next = Substitute.For<IPipe<ConsumeContext<TestMessage>>>();
        next.When(p => p.Send(Arg.Any<ConsumeContext<TestMessage>>()))
            .Do(_ => capturedId = CorrelationContextAccessor.CurrentCorrelationId);

        await filter.Send(context, next);

        capturedId.ShouldBe("inbound-id");
    }

    [Fact]
    public async Task Send_NoInboundHeader_FallsBackToGeneratedId()
    {
        var filter = CreateFilter();
        var context = CreateContext(headerValue: null);
        string? capturedId = null;
        var next = Substitute.For<IPipe<ConsumeContext<TestMessage>>>();
        next.When(p => p.Send(Arg.Any<ConsumeContext<TestMessage>>()))
            .Do(_ => capturedId = CorrelationContextAccessor.CurrentCorrelationId);

        await filter.Send(context, next);

        capturedId.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Send_CustomHeaderName_ReadsFromConfiguredHeader()
    {
        var filter = CreateFilter(headerName: "X-Request-Id");
        var context = CreateContext("custom-id", headerName: "X-Request-Id");
        string? capturedId = null;
        var next = Substitute.For<IPipe<ConsumeContext<TestMessage>>>();
        next.When(p => p.Send(Arg.Any<ConsumeContext<TestMessage>>()))
            .Do(_ => capturedId = CorrelationContextAccessor.CurrentCorrelationId);

        await filter.Send(context, next);

        capturedId.ShouldBe("custom-id");
    }

    [Fact]
    public async Task Send_RestoresCorrelationIdAfterCompletion()
    {
        CorrelationContextAccessor.CurrentCorrelationId = "outer-id";
        var filter = CreateFilter();
        var context = CreateContext("inner-id");
        var next = Substitute.For<IPipe<ConsumeContext<TestMessage>>>();

        await filter.Send(context, next);

        CorrelationContextAccessor.CurrentCorrelationId.ShouldBe("outer-id");
    }

    [Fact]
    public async Task Send_RestoresCorrelationIdEvenWhenNextThrows()
    {
        CorrelationContextAccessor.CurrentCorrelationId = "outer-id";
        var filter = CreateFilter();
        var context = CreateContext("inner-id");
        var next = Substitute.For<IPipe<ConsumeContext<TestMessage>>>();
        next.When(p => p.Send(Arg.Any<ConsumeContext<TestMessage>>()))
            .Do(_ => throw new InvalidOperationException("boom"));

        await Should.ThrowAsync<InvalidOperationException>(() => filter.Send(context, next));

        CorrelationContextAccessor.CurrentCorrelationId.ShouldBe("outer-id");
    }

    [Fact]
    public async Task Send_BeginScopeCalledWithCorrelationIdKey()
    {
        var logger = Substitute.For<ILogger<CorrelationConsumeFilter<TestMessage>>>();
        object? capturedScope = null;
        logger.BeginScope(Arg.Do<object>(s => capturedScope = s))
            .Returns(Substitute.For<IDisposable>());

        var filter = CreateFilter(logger: logger);
        var context = CreateContext("scope-test-id");
        var next = Substitute.For<IPipe<ConsumeContext<TestMessage>>>();

        await filter.Send(context, next);

        capturedScope.ShouldNotBeNull();
        var dict = capturedScope.ShouldBeOfType<Dictionary<string, object?>>();
        dict.ShouldContainKey("CorrelationId");
        dict.ShouldContainKey("TraceId");
        dict.ShouldContainKey("SpanId");
    }

    public sealed class TestMessage { }
}
