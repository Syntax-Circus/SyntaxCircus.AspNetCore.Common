using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace SyntaxCircus.AspNetCore.Common.Tests;

public class HealthCheckEndpointExtensionsTests
{
    [Fact]
    public void MapStandardHealthChecks_NullEndpoints_ThrowsArgumentNullException()
    {
        Should.Throw<ArgumentNullException>(() => HealthCheckEndpointExtensions.MapStandardHealthChecks(null!));
    }

    private static TestServer CreateServer()
    {
        return TestServerFactory.Create(
            services => services.AddHealthChecks()
                .AddCheck("ready-check", () => HealthCheckResult.Healthy(), ["ready"])
                .AddCheck("startup-check", () => HealthCheckResult.Healthy(), ["startup"]),
            app =>
            {
                app.UseRouting();
                app.MapStandardHealthChecks();
            });
    }

    [Fact]
    public async Task GetHealthLive_RunsNoChecks()
    {
        using var server = CreateServer();
        using var client = server.CreateClient();

        var body = await client.GetStringAsync(new Uri("/health/live", UriKind.Relative), TestContext.Current.CancellationToken);

        body.ShouldContain("\"checks\":[]");
    }

    [Fact]
    public async Task GetHealthReady_OnlyRunsReadyTaggedChecks()
    {
        using var server = CreateServer();
        using var client = server.CreateClient();

        var body = await client.GetStringAsync(new Uri("/health/ready", UriKind.Relative), TestContext.Current.CancellationToken);

        body.ShouldContain("ready-check");
        body.ShouldNotContain("startup-check");
    }

    [Fact]
    public async Task GetHealthReady_CustomReadyTag_UsesIt()
    {
        using var server = TestServerFactory.Create(
            services => services.AddHealthChecks()
                .AddCheck("custom-check", () => HealthCheckResult.Healthy(), ["custom-ready"]),
            app =>
            {
                app.UseRouting();
                app.MapStandardHealthChecks(readyTag: "custom-ready");
            });
        using var client = server.CreateClient();

        var body = await client.GetStringAsync(new Uri("/health/ready", UriKind.Relative), TestContext.Current.CancellationToken);

        body.ShouldContain("custom-check");
    }

    [Fact]
    public async Task GetHealthLive_MetadataFactoryProvided_IncludedInResponse()
    {
        using var server = TestServerFactory.Create(
            services => services.AddHealthChecks(),
            app =>
            {
                app.UseRouting();
                app.MapStandardHealthChecks(metadataFactory: _ => new Dictionary<string, object?> { ["version"] = "1.2.3" });
            });
        using var client = server.CreateClient();

        var body = await client.GetStringAsync(new Uri("/health/live", UriKind.Relative), TestContext.Current.CancellationToken);

        body.ShouldContain("\"metadata\":{\"version\":\"1.2.3\"}");
    }

    [Fact]
    public async Task GetHealthLive_NoMetadataFactory_MetadataKeyOmitted()
    {
        using var server = CreateServer();
        using var client = server.CreateClient();

        var body = await client.GetStringAsync(new Uri("/health/live", UriKind.Relative), TestContext.Current.CancellationToken);

        body.ShouldNotContain("metadata");
    }

    [Fact]
    public async Task CustomPaths_MapsToGivenPaths_NotDefaults()
    {
        using var server = TestServerFactory.Create(
            services => services.AddHealthChecks()
                .AddCheck("ready-check", () => HealthCheckResult.Healthy(), ["ready"]),
            app =>
            {
                app.UseRouting();
                app.MapStandardHealthChecks(livePath: "/healthz", readyPath: "/health");
            });
        using var client = server.CreateClient();

        var liveResponse = await client.GetAsync(new Uri("/healthz", UriKind.Relative), TestContext.Current.CancellationToken);
        var readyResponse = await client.GetAsync(new Uri("/health", UriKind.Relative), TestContext.Current.CancellationToken);
        var defaultLiveResponse = await client.GetAsync(new Uri("/health/live", UriKind.Relative), TestContext.Current.CancellationToken);

        liveResponse.StatusCode.ShouldBe(System.Net.HttpStatusCode.OK);
        readyResponse.StatusCode.ShouldBe(System.Net.HttpStatusCode.OK);
        defaultLiveResponse.StatusCode.ShouldBe(System.Net.HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetHealthLive_AsyncMetadataFactoryProvided_IncludedInResponse()
    {
        using var server = TestServerFactory.Create(
            services => services.AddHealthChecks(),
            app =>
            {
                app.UseRouting();
                app.MapStandardHealthChecks(metadataFactoryAsync: _ =>
                    Task.FromResult<IReadOnlyDictionary<string, object?>>(new Dictionary<string, object?> { ["version"] = "1.2.3" }));
            });
        using var client = server.CreateClient();

        var body = await client.GetStringAsync(new Uri("/health/live", UriKind.Relative), TestContext.Current.CancellationToken);

        body.ShouldContain("\"metadata\":{\"version\":\"1.2.3\"}");
    }

    [Fact]
    public async Task GetHealthLive_BothMetadataFactoriesProvided_AsyncTakesPrecedence()
    {
        using var server = TestServerFactory.Create(
            services => services.AddHealthChecks(),
            app =>
            {
                app.UseRouting();
                app.MapStandardHealthChecks(
                    metadataFactory: _ => new Dictionary<string, object?> { ["version"] = "sync" },
                    metadataFactoryAsync: _ =>
                        Task.FromResult<IReadOnlyDictionary<string, object?>>(new Dictionary<string, object?> { ["version"] = "async" }));
            });
        using var client = server.CreateClient();

        var body = await client.GetStringAsync(new Uri("/health/live", UriKind.Relative), TestContext.Current.CancellationToken);

        body.ShouldContain("\"metadata\":{\"version\":\"async\"}");
    }
}
