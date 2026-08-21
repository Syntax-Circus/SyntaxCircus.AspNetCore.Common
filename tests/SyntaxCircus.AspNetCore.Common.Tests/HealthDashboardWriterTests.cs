using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace SyntaxCircus.AspNetCore.Common.Tests;

public class HealthDashboardWriterTests
{
    [Fact]
    public async Task WriteHtmlAsync_NullContext_ThrowsArgumentNullException()
    {
        var report = new HealthReport(new Dictionary<string, HealthReportEntry>(), TimeSpan.Zero);

        await Should.ThrowAsync<ArgumentNullException>(
            () => HealthDashboardWriter.WriteHtmlAsync(null!, report, new HealthDashboardOptions()));
    }

    [Fact]
    public async Task WriteHtmlAsync_NullReport_ThrowsArgumentNullException()
    {
        var context = new DefaultHttpContext { Response = { Body = new MemoryStream() } };

        await Should.ThrowAsync<ArgumentNullException>(
            () => HealthDashboardWriter.WriteHtmlAsync(context, null!, new HealthDashboardOptions()));
    }

    [Fact]
    public async Task WriteHtmlAsync_NullOptions_ThrowsArgumentNullException()
    {
        var context = new DefaultHttpContext { Response = { Body = new MemoryStream() } };
        var report = new HealthReport(new Dictionary<string, HealthReportEntry>(), TimeSpan.Zero);

        await Should.ThrowAsync<ArgumentNullException>(
            () => HealthDashboardWriter.WriteHtmlAsync(context, report, null!));
    }

    [Fact]
    public async Task WriteHtmlAsync_SetsNoStoreCacheHeadersAndHtmlContentType()
    {
        var context = new DefaultHttpContext { Response = { Body = new MemoryStream() } };
        var report = new HealthReport(new Dictionary<string, HealthReportEntry>(), TimeSpan.Zero);

        await HealthDashboardWriter.WriteHtmlAsync(context, report, new HealthDashboardOptions());

        context.Response.Headers.CacheControl.ToString().ShouldBe("no-store");
        context.Response.Headers.Pragma.ToString().ShouldBe("no-cache");
        context.Response.ContentType.ShouldStartWith("text/html");
    }
}
