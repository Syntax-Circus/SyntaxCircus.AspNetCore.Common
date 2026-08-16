namespace SyntaxCircus.AspNetCore.Common.Tests;

public class CorrelationContextAccessorTests : IDisposable
{
    public void Dispose()
    {
        CorrelationContextAccessor.CurrentCorrelationId = null;
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void ResolveCorrelationId_CurrentCorrelationIdSet_ReturnsIt()
    {
        CorrelationContextAccessor.CurrentCorrelationId = "current-id";

        CorrelationContextAccessor.ResolveCorrelationId("fallback").ShouldBe("current-id");
    }

    [Fact]
    public void ResolveCorrelationId_NoCurrentButFallbackProvided_ReturnsFallback()
    {
        CorrelationContextAccessor.CurrentCorrelationId = null;

        CorrelationContextAccessor.ResolveCorrelationId("fallback-id").ShouldBe("fallback-id");
    }

    [Fact]
    public void ResolveCorrelationId_NoCurrentNoFallbackNoActivity_ReturnsNewGuid()
    {
        CorrelationContextAccessor.CurrentCorrelationId = null;

        var result = CorrelationContextAccessor.ResolveCorrelationId();

        Guid.TryParseExact(result, "N", out _).ShouldBeTrue();
    }

    [Fact]
    public void ResolveCorrelationId_NoCurrentNoFallbackWithActivity_ReturnsTraceId()
    {
        CorrelationContextAccessor.CurrentCorrelationId = null;
        using var activity = new System.Diagnostics.Activity("test").Start();

        var result = CorrelationContextAccessor.ResolveCorrelationId();

        result.ShouldBe(activity.TraceId.ToString());
    }
}
