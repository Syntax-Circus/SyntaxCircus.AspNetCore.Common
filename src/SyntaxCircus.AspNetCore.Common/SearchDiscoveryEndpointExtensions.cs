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

        return endpoints.MapRobotsTxt(context => Task.FromResult(contentFactory(context)), route, cacheDuration);
    }

    public static IEndpointRouteBuilder MapRobotsTxt(
        this IEndpointRouteBuilder endpoints,
        Func<HttpContext, Task<string>> contentFactory,
        string route = "/robots.txt",
        TimeSpan? cacheDuration = null)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        ArgumentNullException.ThrowIfNull(contentFactory);

        endpoints.MapGet(route, async (HttpContext context, IOptions<SearchIndexingOptions> options) =>
                {
                    ApplyCacheControl(context, cacheDuration);

                    var content = options.Value.BlockRobotsAndSitemap
                        ? SearchIndexingOptions.DisallowAllRobotsTxt
                        : await contentFactory(context);

                    return Results.Text(content, "text/plain");
                })
            .AllowAnonymous();

        return endpoints;
    }

    public static IEndpointRouteBuilder MapSitemap(
        this IEndpointRouteBuilder endpoints,
        Func<HttpContext, IReadOnlyList<SitemapEntry>> entriesFactory,
        string route = "/sitemap.xml",
        TimeSpan? cacheDuration = null)
    {
        ArgumentNullException.ThrowIfNull(entriesFactory);

        return endpoints.MapSitemap(context => Task.FromResult(entriesFactory(context)), route, cacheDuration);
    }

    public static IEndpointRouteBuilder MapSitemap(
        this IEndpointRouteBuilder endpoints,
        Func<HttpContext, Task<IReadOnlyList<SitemapEntry>>> entriesFactory,
        string route = "/sitemap.xml",
        TimeSpan? cacheDuration = null)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        ArgumentNullException.ThrowIfNull(entriesFactory);

        endpoints.MapGet(route, async (HttpContext context, IOptions<SearchIndexingOptions> options) =>
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
