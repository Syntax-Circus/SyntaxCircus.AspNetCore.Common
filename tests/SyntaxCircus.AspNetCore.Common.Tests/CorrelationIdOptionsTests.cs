namespace SyntaxCircus.AspNetCore.Common.Tests;

public class CorrelationIdOptionsTests
{
    [Fact]
    public void Defaults_HeaderNameIsXCorrelationId()
    {
        new CorrelationIdOptions().HeaderName.ShouldBe("X-Correlation-Id");
    }
}
