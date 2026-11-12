using System.Globalization;
using System.Xml.Linq;

namespace SyntaxCircus.AspNetCore.Common;

public static class SearchDiscoveryEndpointExtensions
{
    public static IEndpointRouteBuilder MapRobotsTxt(
        this IEndpointRouteBuilder endpoints,
        Func<HttpContext, string> contentFactory,
        string route = "/robots.txt",
        TimeSpan? cacheDuration = null)
    {
        ArgumentNullException.ThrowIfNull(contentFactory);

        return MapRobotsTxtCore(endpoints, context => Task.FromResult(contentFactory(context)), null, route, cacheDuration);
    }

    public static IEndpointRouteBuilder MapRobotsTxt(
        this IEndpointRouteBuilder endpoints,
        Func<HttpContext, Task<string>> contentFactory,
        string route = "/robots.txt",
        TimeSpan? cacheDuration = null)
        => MapRobotsTxtCore(endpoints, contentFactory, null, route, cacheDuration);

    /// <summary>
    /// Maps <c>/robots.txt</c> and lets the caller add endpoint conventions (for example
    /// <c>DisableRateLimiting()</c>) to the mapped endpoint. <c>AllowAnonymous</c> stays applied.
    /// </summary>
    public static IEndpointRouteBuilder MapRobotsTxt(
        this IEndpointRouteBuilder endpoints,
        Func<HttpContext, string> contentFactory,
        Action<IEndpointConventionBuilder> configureEndpoint,
        string route = "/robots.txt",
        TimeSpan? cacheDuration = null)
    {
        ArgumentNullException.ThrowIfNull(contentFactory);
        ArgumentNullException.ThrowIfNull(configureEndpoint);

        return MapRobotsTxtCore(endpoints, context => Task.FromResult(contentFactory(context)), configureEndpoint, route, cacheDuration);
    }

    /// <summary>Async variant of the <c>configureEndpoint</c> overload of <c>MapRobotsTxt</c>.</summary>
    public static IEndpointRouteBuilder MapRobotsTxt(
        this IEndpointRouteBuilder endpoints,
        Func<HttpContext, Task<string>> contentFactory,
        Action<IEndpointConventionBuilder> configureEndpoint,
        string route = "/robots.txt",
        TimeSpan? cacheDuration = null)
    {
        ArgumentNullException.ThrowIfNull(configureEndpoint);

        return MapRobotsTxtCore(endpoints, contentFactory, configureEndpoint, route, cacheDuration);
    }

    public static IEndpointRouteBuilder MapSitemap(
        this IEndpointRouteBuilder endpoints,
        Func<HttpContext, IReadOnlyList<SitemapEntry>> entriesFactory,
        string route = "/sitemap.xml",
        TimeSpan? cacheDuration = null)
    {
        ArgumentNullException.ThrowIfNull(entriesFactory);

        return MapSitemapCore(endpoints, context => Task.FromResult(entriesFactory(context)), null, route, cacheDuration);
    }

    public static IEndpointRouteBuilder MapSitemap(
        this IEndpointRouteBuilder endpoints,
        Func<HttpContext, Task<IReadOnlyList<SitemapEntry>>> entriesFactory,
        string route = "/sitemap.xml",
        TimeSpan? cacheDuration = null)
        => MapSitemapCore(endpoints, entriesFactory, null, route, cacheDuration);

    /// <summary>
    /// Maps <c>/sitemap.xml</c> and lets the caller add endpoint conventions (for example
    /// <c>DisableRateLimiting()</c>) to the mapped endpoint. <c>AllowAnonymous</c> stays applied.
    /// </summary>
    public static IEndpointRouteBuilder MapSitemap(
        this IEndpointRouteBuilder endpoints,
        Func<HttpContext, IReadOnlyList<SitemapEntry>> entriesFactory,
        Action<IEndpointConventionBuilder> configureEndpoint,
        string route = "/sitemap.xml",
        TimeSpan? cacheDuration = null)
    {
        ArgumentNullException.ThrowIfNull(entriesFactory);
        ArgumentNullException.ThrowIfNull(configureEndpoint);

        return MapSitemapCore(endpoints, context => Task.FromResult(entriesFactory(context)), configureEndpoint, route, cacheDuration);
    }

    /// <summary>Async variant of the <c>configureEndpoint</c> overload of <c>MapSitemap</c>.</summary>
    public static IEndpointRouteBuilder MapSitemap(
        this IEndpointRouteBuilder endpoints,
        Func<HttpContext, Task<IReadOnlyList<SitemapEntry>>> entriesFactory,
        Action<IEndpointConventionBuilder> configureEndpoint,
        string route = "/sitemap.xml",
        TimeSpan? cacheDuration = null)
    {
        ArgumentNullException.ThrowIfNull(configureEndpoint);

        return MapSitemapCore(endpoints, entriesFactory, configureEndpoint, route, cacheDuration);
    }

    private static IEndpointRouteBuilder MapRobotsTxtCore(
        IEndpointRouteBuilder endpoints,
        Func<HttpContext, Task<string>> contentFactory,
        Action<IEndpointConventionBuilder>? configureEndpoint,
        string route,
        TimeSpan? cacheDuration)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        ArgumentNullException.ThrowIfNull(contentFactory);

        var builder = endpoints.MapGet(route, async (HttpContext context, IOptions<SearchIndexingOptions> options) =>
                {
                    ApplyCacheControl(context, cacheDuration);

                    var content = options.Value.BlockRobotsAndSitemap
                        ? SearchIndexingOptions.DisallowAllRobotsTxt
                        : await contentFactory(context);

                    return Results.Text(content, "text/plain");
                })
            .AllowAnonymous();

        configureEndpoint?.Invoke(builder);

        return endpoints;
    }

    private static IEndpointRouteBuilder MapSitemapCore(
        IEndpointRouteBuilder endpoints,
        Func<HttpContext, Task<IReadOnlyList<SitemapEntry>>> entriesFactory,
        Action<IEndpointConventionBuilder>? configureEndpoint,
        string route,
        TimeSpan? cacheDuration)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        ArgumentNullException.ThrowIfNull(entriesFactory);

        var builder = endpoints.MapGet(route, async (HttpContext context, IOptions<SearchIndexingOptions> options) =>
                {
                    if (options.Value.BlockRobotsAndSitemap)
                    {
                        return Results.NotFound();
                    }

                    ApplyCacheControl(context, cacheDuration);

                    var entries = await entriesFactory(context);
                    return Results.Content(BuildSitemapXml(entries), "application/xml");
                })
            .AllowAnonymous();

        configureEndpoint?.Invoke(builder);

        return endpoints;
    }

    private static void ApplyCacheControl(HttpContext context, TimeSpan? cacheDuration)
    {
        if (cacheDuration is { } duration)
        {
            context.Response.Headers["Cache-Control"] = $"public, max-age={(int)duration.TotalSeconds}";
        }
    }

    private static string BuildSitemapXml(IReadOnlyList<SitemapEntry> entries)
    {
        XNamespace ns = "http://www.sitemaps.org/schemas/sitemap/0.9";

        var urlset = new XElement(ns + "urlset",
            entries.Select(entry =>
            {
                var url = new XElement(ns + "url", new XElement(ns + "loc", entry.Url));

                if (entry.LastModified is { } lastModified)
                {
                    url.Add(new XElement(ns + "lastmod", lastModified.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)));
                }

                if (entry.ChangeFrequency is { } changeFrequency)
                {
                    url.Add(new XElement(ns + "changefreq", changeFrequency.ToString().ToLowerInvariant()));
                }

                if (entry.Priority is { } priority)
                {
                    url.Add(new XElement(ns + "priority", priority.ToString(CultureInfo.InvariantCulture)));
                }

                return url;
            }));

        return new XDocument(new XDeclaration("1.0", "utf-8", null), urlset).ToString();
    }
}

/// <summary>One URL entry rendered by <see cref="SearchDiscoveryEndpointExtensions.MapSitemap"/>.</summary>
public sealed record SitemapEntry(
    string Url,
    DateOnly? LastModified = null,
    SitemapChangeFrequency? ChangeFrequency = null,
    double? Priority = null);

/// <summary>Sitemap protocol <c>&lt;changefreq&gt;</c> values.</summary>
public enum SitemapChangeFrequency
{
    Always,
    Hourly,
    Daily,
    Weekly,
    Monthly,
    Yearly,
    Never,
}
