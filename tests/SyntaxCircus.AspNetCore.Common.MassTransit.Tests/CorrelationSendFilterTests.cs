namespace SyntaxCircus.AspNetCore.Common.MassTransit.Tests;

public sealed class CorrelationSendFilterTests : IDisposable
{
    public void Dispose()
    {
        CorrelationContextAccessor.CurrentCorrelationId = null;
    }

    private static CorrelationSendFilter<TestMessage> CreateFilter(string headerName = "X-Correlation-Id")
    {
        var options = Options.Create(new CorrelationIdOptions { HeaderName = headerName });
        return new CorrelationSendFilter<TestMessage>(options);
    }

    [Fact]
    public async Task Send_SetsOutboundHeaderFromCurrentCorrelationId()
    {
        CorrelationContextAccessor.CurrentCorrelationId = "my-correlation-id";
        var filter = CreateFilter();
        var context = Substitute.For<SendContext<TestMessage>>();
        var headers = Substitute.For<SendHeaders>();
        context.Headers.Returns(headers);
        var next = Substitute.For<IPipe<SendContext<TestMessage>>>();

        await filter.Send(context, next);

        headers.Received(1).Set("X-Correlation-Id", "my-correlation-id");
        await next.Received(1).Send(context);
    }

    [Fact]
    public async Task Send_CustomHeaderName_SetsCorrectHeader()
    {
        CorrelationContextAccessor.CurrentCorrelationId = "my-id";
        var filter = CreateFilter(headerName: "X-Request-Id");
        var context = Substitute.For<SendContext<TestMessage>>();
        var headers = Substitute.For<SendHeaders>();
        context.Headers.Returns(headers);
        var next = Substitute.For<IPipe<SendContext<TestMessage>>>();

        await filter.Send(context, next);

        headers.Received(1).Set("X-Request-Id", "my-id");
    }

    [Fact]
    public async Task Send_NoCurrentCorrelationId_FallsBackToGeneratedId()
    {
        CorrelationContextAccessor.CurrentCorrelationId = null;
        var filter = CreateFilter();
        var context = Substitute.For<SendContext<TestMessage>>();
        var headers = Substitute.For<SendHeaders>();
        context.Headers.Returns(headers);
        var next = Substitute.For<IPipe<SendContext<TestMessage>>>();

        await filter.Send(context, next);

        headers.Received(1).Set(
            "X-Correlation-Id",
            Arg.Is<string>(v => !string.IsNullOrWhiteSpace(v)));
    }

    public sealed class TestMessage { }
}
