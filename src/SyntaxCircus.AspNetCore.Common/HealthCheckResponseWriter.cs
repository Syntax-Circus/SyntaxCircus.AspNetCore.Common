using System.Text.Json;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace SyntaxCircus.AspNetCore.Common;

public static class HealthCheckResponseWriter
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public static Task WriteJsonAsync(HttpContext context, HealthReport report)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(report);

        context.Response.ContentType = "application/json";

        var payload = new
        {
            status = report.Status.ToString(),
            totalDurationMs = report.TotalDuration.TotalMilliseconds,
            checks = report.Entries.Select(entry => new
            {
                name = entry.Key,
                status = entry.Value.Status.ToString(),
                durationMs = entry.Value.Duration.TotalMilliseconds,
                description = entry.Value.Description,
            }),
        };

        return context.Response.WriteAsJsonAsync(payload, SerializerOptions);
    }
}
