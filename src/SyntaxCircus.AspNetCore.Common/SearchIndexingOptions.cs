namespace SyntaxCircus.AspNetCore.Common;

/// <summary>
/// Controls whether a Blazor frontend should discourage search indexing and crawling. Bound from the "SearchIndexing"
/// configuration section.
/// </summary>
public sealed class SearchIndexingOptions
{
    public const string SectionName = "SearchIndexing";
    public const string NoIndexDirective = "noindex,nofollow";

    /// <summary>
    /// Body written by <see cref="SearchDiscoveryEndpointExtensions.MapRobotsTxt"/> when
    /// <see cref="BlockRobotsAndSitemap"/> is true.
    /// </summary>
    public const string DisallowAllRobotsTxt = "User-agent: *\nDisallow: /";

    /// <summary>
    /// Emits noindex metadata in HTML responses and a matching X-Robots-Tag header.
    /// </summary>
    public bool BlockPageMetadata { get; set; }

    /// <summary>
    /// The value written to <c>X-Robots-Tag</c> by the parameterless
    /// <see cref="SearchIndexingHeaderExtensions.UseSearchIndexingHeaders(IApplicationBuilder)"/> overload when
    /// applicable. Defaults to <see cref="NoIndexDirective"/>. The <c>shouldApply</c>-predicate overload always
    /// uses <see cref="NoIndexDirective"/> directly, regardless of this setting — it's the fixed low-level
    /// primitive that this option doesn't affect.
    /// </summary>
    public string RobotsDirective { get; set; } = NoIndexDirective;

    /// <summary>
    /// Blocks crawler discovery endpoints such as robots.txt and sitemap.xml.
    /// </summary>
    public bool BlockRobotsAndSitemap { get; set; }

    /// <summary>
    /// Additional exact request paths (case-insensitive) that never get <c>X-Robots-Tag</c> from the
    /// parameterless <see cref="SearchIndexingHeaderExtensions.UseSearchIndexingHeaders(IApplicationBuilder)"/>
    /// overload, even when <see cref="BlockPageMetadata"/> is true. On top of whatever's listed here, that
    /// overload always skips <c>/robots.txt</c> and <c>/sitemap.xml</c> — those should never carry a noindex
    /// signal themselves — regardless of this list. Defaults to empty, like every other list option in this
    /// package, so config binding only ever adds entries rather than needing to replace a pre-populated default
    /// (list-valued options are additive under <c>IConfiguration</c> binding, not replaced wholesale).
    /// </summary>
    public IReadOnlyList<string> ExcludedPaths { get; set; } = [];
}
