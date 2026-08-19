namespace SyntaxCircus.AspNetCore.Common.Tests;

public class SearchIndexingHeaderExtensionsTests
{
    private static IConfiguration EmptyConfiguration()
        => new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>()).Build();

    [Fact]
    public void AddSearchIndexing_NullServices_ThrowsArgumentNullException()
    {
        Should.Throw<ArgumentNullException>(() =>
            SearchIndexingHeaderExtensions.AddSearchIndexing(null!, EmptyConfiguration()));
    }

    [Fact]
    public void AddSearchIndexing_NullConfiguration_ThrowsArgumentNullException()
    {
        var services = new ServiceCollection();

        Should.Throw<ArgumentNullException>(() => services.AddSearchIndexing(null!));
    }

    [Fact]
    public void UseSearchIndexingHeaders_NullApp_ThrowsArgumentNullException()
    {
        Should.Throw<ArgumentNullException>(() => SearchIndexingHeaderExtensions.UseSearchIndexingHeaders(null!));
    }

    [Fact]
    public void UseSearchIndexingHeaders_NullAppWithPredicate_ThrowsArgumentNullException()
    {
        Should.Throw<ArgumentNullException>(() =>
            SearchIndexingHeaderExtensions.UseSearchIndexingHeaders(null!, _ => true));
    }

    [Fact]
    public void UseSearchIndexingHeaders_NullShouldApply_ThrowsArgumentNullException()
    {
        var app = WebApplication.CreateBuilder().Build();

        Should.Throw<ArgumentNullException>(() => app.UseSearchIndexingHeaders(null!));
    }

    [Fact]
    public async Task UseSearchIndexingHeaders_Parameterless_BlockPageMetadataTrue_SetsHeader()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["SearchIndexing:BlockPageMetadata"] = "true",
        }).Build();

        using var server = TestServerFactory.Create(
            services => services.AddSearchIndexing(configuration),
            app =>
            {
                app.UseSearchIndexingHeaders();
                app.MapGet("/", () => "ok");
            });
        using var client = server.CreateClient();

        var response = await client.GetAsync(new Uri("/", UriKind.Relative), TestContext.Current.CancellationToken);

        response.Headers.GetValues("X-Robots-Tag").ShouldContain("noindex,nofollow");
    }

    [Fact]
    public async Task UseSearchIndexingHeaders_Parameterless_BlockPageMetadataFalse_DoesNotSetHeader()
    {
        using var server = TestServerFactory.Create(
            services => services.AddSearchIndexing(EmptyConfiguration()),
            app =>
            {
                app.UseSearchIndexingHeaders();
                app.MapGet("/", () => "ok");
            });
        using var client = server.CreateClient();

        var response = await client.GetAsync(new Uri("/", UriKind.Relative), TestContext.Current.CancellationToken);

        response.Headers.Contains("X-Robots-Tag").ShouldBeFalse();
    }

    [Fact]
    public async Task UseSearchIndexingHeaders_Predicate_DoesNotOverwriteExistingHeader()
    {
        using var server = TestServerFactory.Create(
            services => services.AddRouting(),
            app =>
            {
                app.Use(async (context, next) =>
                {
                    context.Response.OnStarting(() =>
                    {
                        context.Response.Headers["X-Robots-Tag"] = "index,follow";
                        return Task.CompletedTask;
                    });

                    await next();
                });
                app.UseSearchIndexingHeaders(_ => true);
                app.MapGet("/", () => "ok");
            });
        using var client = server.CreateClient();

        var response = await client.GetAsync(new Uri("/", UriKind.Relative), TestContext.Current.CancellationToken);

        response.Headers.GetValues("X-Robots-Tag").ShouldContain("index,follow");
    }

    [Fact]
    public async Task UseSearchIndexingHeaders_SecurityHeadersRegisteredAfter_SecurityHeadersRobotsTagWins()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["SecurityHeaders:RobotsTag"] = "index,follow",
        }).Build();

        using var server = TestServerFactory.Create(
            services => services.AddSecurityHeaders(configuration),
            app =>
            {
                app.UseSearchIndexingHeaders(_ => true);
                app.UseSecurityHeaders();
                app.MapGet("/", () => "ok");
            });
        using var client = server.CreateClient();

        var response = await client.GetAsync(new Uri("/", UriKind.Relative), TestContext.Current.CancellationToken);

        response.Headers.GetValues("X-Robots-Tag").ShouldContain("index,follow");
    }

    [Fact]
    public async Task UseSearchIndexingHeaders_SecurityHeadersRegisteredBefore_SecurityHeadersRobotsTagWins()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["SecurityHeaders:RobotsTag"] = "index,follow",
        }).Build();

        using var server = TestServerFactory.Create(
            services => services.AddSecurityHeaders(configuration),
            app =>
            {
                app.UseSecurityHeaders();
                app.UseSearchIndexingHeaders(_ => true);
                app.MapGet("/", () => "ok");
            });
        using var client = server.CreateClient();

        var response = await client.GetAsync(new Uri("/", UriKind.Relative), TestContext.Current.CancellationToken);

        response.Headers.GetValues("X-Robots-Tag").ShouldContain("index,follow");
    }

    [Fact]
    public async Task UseSearchIndexingHeaders_Parameterless_DefaultExcludedPaths_SkipsRobotsAndSitemap()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["SearchIndexing:BlockPageMetadata"] = "true",
        }).Build();

        using var server = TestServerFactory.Create(
            services => services.AddSearchIndexing(configuration),
            app =>
            {
                app.UseSearchIndexingHeaders();
                app.MapGet("/robots.txt", () => "ok");
                app.MapGet("/sitemap.xml", () => "ok");
                app.MapGet("/", () => "ok");
            });
        using var client = server.CreateClient();

        var robotsResponse = await client.GetAsync(new Uri("/robots.txt", UriKind.Relative), TestContext.Current.CancellationToken);
        var sitemapResponse = await client.GetAsync(new Uri("/sitemap.xml", UriKind.Relative), TestContext.Current.CancellationToken);
        var rootResponse = await client.GetAsync(new Uri("/", UriKind.Relative), TestContext.Current.CancellationToken);

        robotsResponse.Headers.Contains("X-Robots-Tag").ShouldBeFalse();
        sitemapResponse.Headers.Contains("X-Robots-Tag").ShouldBeFalse();
        rootResponse.Headers.GetValues("X-Robots-Tag").ShouldContain("noindex,nofollow");
    }

    [Fact]
    public async Task UseSearchIndexingHeaders_Parameterless_CustomExcludedPaths_AddsToDefault()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["SearchIndexing:BlockPageMetadata"] = "true",
            ["SearchIndexing:ExcludedPaths:0"] = "/health",
        }).Build();

        using var server = TestServerFactory.Create(
            services => services.AddSearchIndexing(configuration),
            app =>
            {
                app.UseSearchIndexingHeaders();
                app.MapGet("/robots.txt", () => "ok");
                app.MapGet("/health", () => "ok");
                app.MapGet("/", () => "ok");
            });
        using var client = server.CreateClient();

        var robotsResponse = await client.GetAsync(new Uri("/robots.txt", UriKind.Relative), TestContext.Current.CancellationToken);
        var healthResponse = await client.GetAsync(new Uri("/health", UriKind.Relative), TestContext.Current.CancellationToken);
        var rootResponse = await client.GetAsync(new Uri("/", UriKind.Relative), TestContext.Current.CancellationToken);

        robotsResponse.Headers.Contains("X-Robots-Tag").ShouldBeFalse();
        healthResponse.Headers.Contains("X-Robots-Tag").ShouldBeFalse();
        rootResponse.Headers.GetValues("X-Robots-Tag").ShouldContain("noindex,nofollow");
    }

    [Fact]
    public async Task UseSearchIndexingHeaders_Parameterless_CustomRobotsDirective_UsesConfiguredValue()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["SearchIndexing:BlockPageMetadata"] = "true",
            ["SearchIndexing:RobotsDirective"] = "noindex",
        }).Build();

        using var server = TestServerFactory.Create(
            services => services.AddSearchIndexing(configuration),
            app =>
            {
                app.UseSearchIndexingHeaders();
                app.MapGet("/", () => "ok");
            });
        using var client = server.CreateClient();

        var response = await client.GetAsync(new Uri("/", UriKind.Relative), TestContext.Current.CancellationToken);

        response.Headers.GetValues("X-Robots-Tag").ShouldContain("noindex");
    }

    [Fact]
    public async Task UseSearchIndexingHeaders_Predicate_IgnoresConfiguredRobotsDirective()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["SearchIndexing:RobotsDirective"] = "noindex",
        }).Build();

        using var server = TestServerFactory.Create(
            services => services.AddSearchIndexing(configuration),
            app =>
            {
                app.UseSearchIndexingHeaders(_ => true);
                app.MapGet("/", () => "ok");
            });
        using var client = server.CreateClient();

        var response = await client.GetAsync(new Uri("/", UriKind.Relative), TestContext.Current.CancellationToken);

        response.Headers.GetValues("X-Robots-Tag").ShouldContain("noindex,nofollow");
    }
}
