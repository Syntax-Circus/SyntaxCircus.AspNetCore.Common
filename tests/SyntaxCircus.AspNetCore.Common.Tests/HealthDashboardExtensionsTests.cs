using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace SyntaxCircus.AspNetCore.Common.Tests;

public class HealthDashboardExtensionsTests
{
    [Fact]
    public void MapHealthCheckDashboard_NullEndpoints_ThrowsArgumentNullException()
    {
        Should.Throw<ArgumentNullException>(() => HealthDashboardExtensions.MapHealthCheckDashboard(null!));
    }

    [Fact]
    public async Task GetDashboard_DefaultPattern_ReturnsHtmlWithNoStoreCacheControl()
    {
        using var server = TestServerFactory.Create(
            services => services.AddHealthChecks().AddCheck("ready-check", () => HealthCheckResult.Healthy(), ["ready"]),
            app =>
            {
                app.UseRouting();
                app.MapHealthCheckDashboard();
            });
        using var client = server.CreateClient();

        var response = await client.GetAsync(new Uri("/health/dashboard", UriKind.Relative), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(System.Net.HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("text/html");
        response.Headers.CacheControl!.NoStore.ShouldBeTrue();
    }

    [Fact]
    public async Task GetDashboard_UnhealthyReport_StillReturns200()
    {
        using var server = TestServerFactory.Create(
            services => services.AddHealthChecks().AddCheck("bad-check", () => HealthCheckResult.Unhealthy(), ["ready"]),
            app =>
            {
                app.UseRouting();
                app.MapHealthCheckDashboard();
            });
        using var client = server.CreateClient();

        var response = await client.GetAsync(new Uri("/health/dashboard", UriKind.Relative), TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(System.Net.HttpStatusCode.OK);
        body.ShouldContain("badge-unhealthy");
    }

    [Fact]
    public async Task GetDashboard_RespectsReadyTag()
    {
        using var server = TestServerFactory.Create(
            services => services.AddHealthChecks()
                .AddCheck("ready-check", () => HealthCheckResult.Healthy(), ["custom-ready"])
                .AddCheck("startup-check", () => HealthCheckResult.Healthy(), ["startup"]),
            app =>
            {
                app.UseRouting();
                app.MapHealthCheckDashboard(readyTag: "custom-ready");
            });
        using var client = server.CreateClient();

        var body = await client.GetStringAsync(new Uri("/health/dashboard", UriKind.Relative), TestContext.Current.CancellationToken);

        body.ShouldContain("ready-check");
        body.ShouldNotContain("startup-check");
    }

    [Fact]
    public async Task GetDashboard_EmptyOptions_RendersWellFormedPageWithNoOptionalSections()
    {
        using var server = TestServerFactory.Create(
            services => services.AddHealthChecks().AddCheck("ready-check", () => HealthCheckResult.Healthy(), ["ready"]),
            app =>
            {
                app.UseRouting();
                app.MapHealthCheckDashboard();
            });
        using var client = server.CreateClient();

        var body = await client.GetStringAsync(new Uri("/health/dashboard", UriKind.Relative), TestContext.Current.CancellationToken);

        body.ShouldContain("<title>Service Status</title>");
        body.ShouldContain("Health Checks");
        body.ShouldNotContain("<section class=\"notice-box\">");
        body.ShouldNotContain("<div class=\"api-link-box\">");
    }

    [Fact]
    public async Task GetDashboard_ConfigureCallback_RunsPerRequest()
    {
        var counter = 0;
        using var server = TestServerFactory.Create(
            services => services.AddHealthChecks(),
            app =>
            {
                app.UseRouting();
                app.MapHealthCheckDashboard(configure: (_, options) =>
                {
                    counter++;
                    options.Subtitle = $"call #{counter}";
                    return Task.CompletedTask;
                });
            });
        using var client = server.CreateClient();

        var first = await client.GetStringAsync(new Uri("/health/dashboard", UriKind.Relative), TestContext.Current.CancellationToken);
        var second = await client.GetStringAsync(new Uri("/health/dashboard", UriKind.Relative), TestContext.Current.CancellationToken);

        first.ShouldContain("call #1");
        second.ShouldContain("call #2");
    }

    [Fact]
    public async Task GetDashboard_NoticesSectionsAndLinksProvided_Rendered()
    {
        using var server = TestServerFactory.Create(
            services => services.AddHealthChecks(),
            app =>
            {
                app.UseRouting();
                app.MapHealthCheckDashboard(configure: (_, options) =>
                {
                    options.Notices = [new HealthDashboardNotice("Attribution", "Some notice text")];
                    options.Sections = [new HealthDashboardSection("Configuration", [("Mode", "Online")])];
                    options.ApiLinks = [new HealthDashboardLink("JSON report", "/health")];
                    return Task.CompletedTask;
                });
            });
        using var client = server.CreateClient();

        var body = await client.GetStringAsync(new Uri("/health/dashboard", UriKind.Relative), TestContext.Current.CancellationToken);

        body.ShouldContain("Attribution");
        body.ShouldContain("Some notice text");
        body.ShouldContain("Configuration");
        body.ShouldContain("Mode");
        body.ShouldContain("JSON report");
        body.ShouldContain("href=\"/health\"");
    }

    [Fact]
    public async Task GetDashboard_StatusGroupWithDegradedItem_RendersDegradedBadgeInHeaderAndTable()
    {
        using var server = TestServerFactory.Create(
            services => services.AddHealthChecks(),
            app =>
            {
                app.UseRouting();
                app.MapHealthCheckDashboard(configure: (_, options) =>
                {
                    options.StatusGroups =
                    [
                        new HealthDashboardStatusGroup("Background Jobs",
                        [
                            new HealthDashboardStatusItem("grype", "in_progress", HealthStatus.Degraded),
                        ]),
                    ];
                    return Task.CompletedTask;
                });
            });
        using var client = server.CreateClient();

        var body = await client.GetStringAsync(new Uri("/health/dashboard", UriKind.Relative), TestContext.Current.CancellationToken);

        body.ShouldContain("badge badge-sm badge-degraded");
        body.ShouldContain("badge badge-degraded");
        body.ShouldContain("in_progress");
    }

    [Fact]
    public async Task GetDashboard_XssInCheckAndOptionsContent_IsHtmlEncoded()
    {
        using var server = TestServerFactory.Create(
            services => services.AddHealthChecks()
                .AddCheck("<script>evil()</script>", () => HealthCheckResult.Healthy("<img src=x onerror=alert(1)>"), ["ready"]),
            app =>
            {
                app.UseRouting();
                app.MapHealthCheckDashboard(configure: (_, options) =>
                {
                    options.Notices = [new HealthDashboardNotice("<b>title</b>", "<script>alert(2)</script>")];
                    options.Sections = [new HealthDashboardSection("<i>section</i>", [("<u>label</u>", "<script>alert(3)</script>")])];
                    options.StatusGroups =
                    [
                        new HealthDashboardStatusGroup("<span>group</span>",
                        [
                            new HealthDashboardStatusItem("<script>name</script>", "<script>status</script>", HealthStatus.Unhealthy,
                                new Dictionary<string, string?> { ["<script>key</script>"] = "<script>value</script>" }),
                        ]),
                    ];
                    return Task.CompletedTask;
                });
            });
        using var client = server.CreateClient();

        var body = await client.GetStringAsync(new Uri("/health/dashboard", UriKind.Relative), TestContext.Current.CancellationToken);

        body.ShouldNotContain("<script>");
        body.ShouldContain("&lt;script&gt;");
    }
}
