using System.Diagnostics;

namespace SyntaxCircus.AspNetCore.Common;

/// <summary>Ambient (AsyncLocal) access to the current request's correlation ID.</summary>
public static class CorrelationContextAccessor
{
    private static readonly AsyncLocal<string?> CorrelationIdSlot = new();

    public static string? CurrentCorrelationId
    {
        get => CorrelationIdSlot.Value;
        set => CorrelationIdSlot.Value = value;
    }

    public static string? CurrentTraceId => Activity.Current?.TraceId.ToString();

    public static string ResolveCorrelationId(string? fallback = null)
    {
        if (!string.IsNullOrWhiteSpace(CurrentCorrelationId))
        {
            return CurrentCorrelationId;
        }

        if (!string.IsNullOrWhiteSpace(fallback))
        {
            return fallback;
        }

        var traceId = CurrentTraceId;
        return string.IsNullOrWhiteSpace(traceId) ? Guid.NewGuid().ToString("N") : traceId;
    }
}
