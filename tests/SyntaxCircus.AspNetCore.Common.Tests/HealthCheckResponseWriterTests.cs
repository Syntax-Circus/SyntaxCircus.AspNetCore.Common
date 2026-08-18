using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace SyntaxCircus.AspNetCore.Common.Tests;

public class HealthCheckResponseWriterTests
{
    [Fact]
    public async Task WriteJsonAsync_NullContext_ThrowsArgumentNullException()
    {
        var report = new HealthReport(new Dictionary<string, HealthReportEntry>(), TimeSpan.Zero);

        await Should.ThrowAsync<ArgumentNullException>(() => HealthCheckResponseWriter.WriteJsonAsync(null!, report));
    }

    [Fact]
    public async Task WriteJsonAsync_NullReport_ThrowsArgumentNullException()
    {
        var context = new DefaultHttpContext();

        await Should.ThrowAsync<ArgumentNullException>(() => HealthCheckResponseWriter.WriteJsonAsync(context, null!));
    }

    [Fact]
    public async Task WriteJsonAsync_WritesExpectedJsonShape()
    {
        var context = new DefaultHttpContext { Response = { Body = new MemoryStream() } };
        var entries = new Dictionary<string, HealthReportEntry>
        {
            ["db"] = new HealthReportEntry(HealthStatus.Healthy, "database ok", TimeSpan.FromMilliseconds(12), null, null),
        };
        var report = new HealthReport(entries, TimeSpan.FromMilliseconds(20));

        await HealthCheckResponseWriter.WriteJsonAsync(context, report);

        context.Response.ContentType.ShouldStartWith("application/json");
        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body);
        var body = await reader.ReadToEndAsync(TestContext.Current.CancellationToken);

        body.ShouldContain("\"status\":\"Healthy\"");
        body.ShouldContain("\"totalDurationMs\":20");
        body.ShouldContain("\"name\":\"db\"");
        body.ShouldContain("\"description\":\"database ok\"");
    }

    [Fact]
    public async Task WriteJsonAsync_MetadataProvided_IncludedUnderMetadataKey()
    {
        var context = new DefaultHttpContext { Response = { Body = new MemoryStream() } };
        var report = new HealthReport(new Dictionary<string, HealthReportEntry>(), TimeSpan.Zero);
        var metadata = new Dictionary<string, object?> { ["version"] = "1.2.3" };

        await HealthCheckResponseWriter.WriteJsonAsync(context, report, metadata);

        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body);
        var body = await reader.ReadToEndAsync(TestContext.Current.CancellationToken);

        body.ShouldContain("\"metadata\":{\"version\":\"1.2.3\"}");
    }

    [Fact]
    public async Task WriteJsonAsync_NoMetadata_KeyOmittedEntirely()
    {
        var context = new DefaultHttpContext { Response = { Body = new MemoryStream() } };
        var report = new HealthReport(new Dictionary<string, HealthReportEntry>(), TimeSpan.Zero);

        await HealthCheckResponseWriter.WriteJsonAsync(context, report);

        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body);
        var body = await reader.ReadToEndAsync(TestContext.Current.CancellationToken);

        body.ShouldNotContain("metadata");
    }

    [Fact]
    public async Task WriteJsonAsync_NullDescription_StillSerializesAsNull()
    {
        var context = new DefaultHttpContext { Response = { Body = new MemoryStream() } };
        var entries = new Dictionary<string, HealthReportEntry>
        {
            ["db"] = new HealthReportEntry(HealthStatus.Healthy, null, TimeSpan.FromMilliseconds(5), null, null),
        };
        var report = new HealthReport(entries, TimeSpan.FromMilliseconds(5));

        await HealthCheckResponseWriter.WriteJsonAsync(context, report);

        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body);
        var body = await reader.ReadToEndAsync(TestContext.Current.CancellationToken);

        body.ShouldContain("\"description\":null");
    }
}
