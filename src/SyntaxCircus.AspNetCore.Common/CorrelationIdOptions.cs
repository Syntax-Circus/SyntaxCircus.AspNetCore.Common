namespace SyntaxCircus.AspNetCore.Common;

public sealed class CorrelationIdOptions
{
    public string HeaderName { get; set; } = "X-Correlation-Id";
}
