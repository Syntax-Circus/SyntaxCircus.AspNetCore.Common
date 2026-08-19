namespace SyntaxCircus.AspNetCore.Common.Tests;

public class SearchDiscoveryEndpointExtensionsTests
{
    private static IConfiguration EmptyConfiguration()
        => new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>()).Build();

    [Fact]
    public void MapRobotsTxt_NullEndpoints_ThrowsArgumentNullException()
    {
        Should.Throw<ArgumentNullException>(() =>
            SearchDiscoveryEndpointExtensions.MapRobotsTxt(null!, _ => "ok"));
    }

    [Fact]
    public void MapRobotsTxt_NullContentFactory_ThrowsArgumentNullException()
    {
        using var server = TestServerFactory.Create(
            services => services.AddRouting(),
            app => Should.Throw<ArgumentNullException>(() => app.MapRobotsTxt((Func<HttpContext, string>)null!)));
    }

    [Fact]
    public void MapRobotsTxt_NullAsyncContentFactory_ThrowsArgumentNullException()
    {
        using var server = TestServerFactory.Create(
            services => services.AddRouting(),
            app => Should.Throw<ArgumentNullException>(() => app.MapRobotsTxt((Func<HttpContext, Task<string>>)null!)));
    }

    [Fact]
    public void MapSitemap_NullEndpoints_ThrowsArgumentNullException()
    {
        Should.Throw<ArgumentNullException>(() =>
            SearchDiscoveryEndpointExtensions.MapSitemap(null!, _ => []));
    }

    [Fact]
    public void MapSitemap_NullEntriesFactory_ThrowsArgumentNullException()
    {
        using var server = TestServerFactory.Create(
            services => services.AddRouting(),
            app => Should.Throw<ArgumentNullException>(() => app.MapSitemap((Func<HttpContext, IReadOnlyList<SitemapEntry>>)null!)));
    }

    [Fact]
    public void MapSitemap_NullAsyncEntriesFactory_ThrowsArgumentNullException()
    {
        using var server = TestServerFactory.Create(
            services => services.AddRouting(),
            app => Should.Throw<ArgumentNullException>(() =>
                app.MapSitemap((Func<HttpContext, Task<IReadOnlyList<SitemapEntry>>>)null!)));
    }

    [Fact]
    public async Task MapRobotsTxt_NotBlocked_ReturnsAppContent()
    {
        using var server = TestServerFactory.Create(
            services => services.AddSearchIndexing(EmptyConfiguration()),
            app => app.MapRobotsTxt(_ => "User-agent: *\nAllow: /"));
        using var client = server.CreateClient();

        var response = await client.GetAsync(new Uri("/robots.txt", UriKind.Relative), TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        response.IsSuccessStatusCode.ShouldBeTrue();
        body.ShouldBe("User-agent: *\nAllow: /");
    }

    [Fact]
    public async Task MapRobotsTxt_Blocked_ReturnsDisallowAll()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["SearchIndexing:BlockRobotsAndSitemap"] = "true",
        }).Build();

        using var server = TestServerFactory.Create(
            services => services.AddSearchIndexing(configuration),
            app => app.MapRobotsTxt(_ => "User-agent: *\nAllow: /"));
        using var client = server.CreateClient();

        var response = await client.GetAsync(new Uri("/robots.txt", UriKind.Relative), TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        body.ShouldBe(SearchIndexingOptions.DisallowAllRobotsTxt);
    }

    [Fact]
    public async Task MapRobotsTxt_AsyncContentFactory_ReturnsAwaitedContent()
    {
        using var server = TestServerFactory.Create(
            services => services.AddSearchIndexing(EmptyConfiguration()),
            app => app.MapRobotsTxt(async _ =>
            {
                await Task.Delay(1, TestContext.Current.CancellationToken);
                return "User-agent: *\nAllow: /async";
            }));
        using var client = server.CreateClient();

        var response = await client.GetAsync(new Uri("/robots.txt", UriKind.Relative), TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        body.ShouldBe("User-agent: *\nAllow: /async");
    }

    [Fact]
    public async Task MapRobotsTxt_CacheDurationSet_SetsCacheControlHeader()
    {
        using var server = TestServerFactory.Create(
            services => services.AddSearchIndexing(EmptyConfiguration()),
            app => app.MapRobotsTxt(_ => "User-agent: *\nAllow: /", cacheDuration: TimeSpan.FromHours(1)));
        using var client = server.CreateClient();

        var response = await client.GetAsync(new Uri("/robots.txt", UriKind.Relative), TestContext.Current.CancellationToken);

        response.Headers.CacheControl.ShouldNotBeNull();
        response.Headers.CacheControl!.ToString().ShouldBe("public, max-age=3600");
    }

    [Fact]
    public async Task MapRobotsTxt_CacheDurationNotSet_NoCacheControlHeader()
    {
        using var server = TestServerFactory.Create(
            services => services.AddSearchIndexing(EmptyConfiguration()),
            app => app.MapRobotsTxt(_ => "User-agent: *\nAllow: /"));
        using var client = server.CreateClient();

        var response = await client.GetAsync(new Uri("/robots.txt", UriKind.Relative), TestContext.Current.CancellationToken);

        response.Headers.CacheControl.ShouldBeNull();
    }

    [Fact]
    public async Task MapSitemap_NotBlocked_ReturnsEscapedXml()
    {
        using var server = TestServerFactory.Create(
            services => services.AddSearchIndexing(EmptyConfiguration()),
            app => app.MapSitemap(_ =>
            [
                new SitemapEntry("https://example.com/?a=1&b=2", new DateOnly(2026, 1, 15)),
            ]));
        using var client = server.CreateClient();

        var response = await client.GetAsync(new Uri("/sitemap.xml", UriKind.Relative), TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        response.IsSuccessStatusCode.ShouldBeTrue();
        body.ShouldContain("<loc>https://example.com/?a=1&amp;b=2</loc>");
        body.ShouldContain("<lastmod>2026-01-15</lastmod>");
    }

    [Fact]
    public async Task MapSitemap_ChangeFrequencyAndPrioritySet_RendersBoth()
    {
        using var server = TestServerFactory.Create(
            services => services.AddSearchIndexing(EmptyConfiguration()),
            app => app.MapSitemap(_ =>
            [
                new SitemapEntry("https://example.com/", ChangeFrequency: SitemapChangeFrequency.Weekly, Priority: 0.8),
            ]));
        using var client = server.CreateClient();

        var response = await client.GetAsync(new Uri("/sitemap.xml", UriKind.Relative), TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        body.ShouldContain("<changefreq>weekly</changefreq>");
        body.ShouldContain("<priority>0.8</priority>");
    }

    [Fact]
    public async Task MapSitemap_ChangeFrequencyAndPriorityUnset_OmitsBoth()
    {
        using var server = TestServerFactory.Create(
            services => services.AddSearchIndexing(EmptyConfiguration()),
            app => app.MapSitemap(_ => [new SitemapEntry("https://example.com/")]));
        using var client = server.CreateClient();

        var response = await client.GetAsync(new Uri("/sitemap.xml", UriKind.Relative), TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        body.ShouldNotContain("changefreq");
        body.ShouldNotContain("priority");
    }

    [Fact]
    public async Task MapSitemap_AsyncEntriesFactory_ReturnsAwaitedEntries()
    {
        using var server = TestServerFactory.Create(
            services => services.AddSearchIndexing(EmptyConfiguration()),
            app => app.MapSitemap(async _ =>
            {
                await Task.Delay(1, TestContext.Current.CancellationToken);
                return (IReadOnlyList<SitemapEntry>) [new SitemapEntry("https://example.com/async")];
            }));
        using var client = server.CreateClient();

        var response = await client.GetAsync(new Uri("/sitemap.xml", UriKind.Relative), TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        body.ShouldContain("<loc>https://example.com/async</loc>");
    }

    [Fact]
    public async Task MapSitemap_CacheDurationSet_SetsCacheControlHeader()
    {
        using var server = TestServerFactory.Create(
            services => services.AddSearchIndexing(EmptyConfiguration()),
            app => app.MapSitemap(_ => [new SitemapEntry("https://example.com/")], cacheDuration: TimeSpan.FromMinutes(30)));
        using var client = server.CreateClient();

        var response = await client.GetAsync(new Uri("/sitemap.xml", UriKind.Relative), TestContext.Current.CancellationToken);

        response.Headers.CacheControl.ShouldNotBeNull();
        response.Headers.CacheControl!.ToString().ShouldBe("public, max-age=1800");
    }

    [Fact]
    public async Task MapSitemap_Blocked_ReturnsNotFound()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["SearchIndexing:BlockRobotsAndSitemap"] = "true",
        }).Build();

        using var server = TestServerFactory.Create(
            services => services.AddSearchIndexing(configuration),
            app => app.MapSitemap(_ => [new SitemapEntry("https://example.com/")], cacheDuration: TimeSpan.FromMinutes(30)));
        using var client = server.CreateClient();

        var response = await client.GetAsync(new Uri("/sitemap.xml", UriKind.Relative), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(System.Net.HttpStatusCode.NotFound);
        response.Headers.CacheControl.ShouldBeNull();
    }
}
