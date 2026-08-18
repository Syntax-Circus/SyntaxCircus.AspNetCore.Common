using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace SyntaxCircus.AspNetCore.Common;

public static class HealthCheckResponseWriter
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Writes the standard health-check JSON shape. <paramref name="metadata"/> is optional, caller-supplied
    /// extra data (e.g. an app version) rendered under a top-level <c>metadata</c> key — omitted entirely
    /// when null, so existing callers' output is unchanged. Unlike <c>metadata</c>, a null per-check
    /// <c>description</c> still serializes as <c>null</c>, matching prior behavior.
    /// </summary>
    public static Task WriteJsonAsync(HttpContext context, HealthReport report, IReadOnlyDictionary<string, object?>? metadata = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(report);

        context.Response.ContentType = "application/json";

        var payload = new HealthCheckPayload(
            report.Status.ToString(),
            report.TotalDuration.TotalMilliseconds,
            report.Entries.Select(entry => new HealthCheckEntryPayload(
                entry.Key,
                entry.Value.Status.ToString(),
                entry.Value.Duration.TotalMilliseconds,
                entry.Value.Description)),
            metadata);

        return context.Response.WriteAsJsonAsync(payload, SerializerOptions);
    }

    private sealed record HealthCheckPayload(
        string Status,
        double TotalDurationMs,
        IEnumerable<HealthCheckEntryPayload> Checks,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyDictionary<string, object?>? Metadata);

    private sealed record HealthCheckEntryPayload(string Name, string Status, double DurationMs, string? Description);
}
