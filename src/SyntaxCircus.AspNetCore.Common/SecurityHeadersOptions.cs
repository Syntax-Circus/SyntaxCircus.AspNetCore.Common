namespace SyntaxCircus.AspNetCore.Common;

public sealed class SecurityHeadersOptions
{
    public const string SectionName = "SecurityHeaders";

    public string ReferrerPolicy { get; init; } = "strict-origin-when-cross-origin";

    public string FrameOptions { get; init; } = "DENY";

    public string ContentTypeOptions { get; init; } = "nosniff";

    public string PermissionsPolicy { get; init; } = "camera=(), geolocation=(), microphone=()";

    public string ContentSecurityPolicy { get; init; } =
        "base-uri 'self'; form-action 'self'; frame-ancestors 'none'; upgrade-insecure-requests";

    public string StrictTransportSecurity { get; init; } = "max-age=31536000; includeSubDomains";

    /// <summary>
    /// Value for the <c>X-Robots-Tag</c> response header (e.g. <c>"noindex, nofollow"</c>). Null (the
    /// default) omits the header entirely — most routes don't want it set.
    /// </summary>
    public string? RobotsTag { get; init; }

    /// <summary>
    /// Per-path overrides for <see cref="ReferrerPolicy"/> and <see cref="RobotsTag"/>, checked in order —
    /// the first entry whose <see cref="SecurityHeadersPathOverride.PathPrefix"/> matches the request path
    /// wins. Useful for a route that needs different values than the rest of the app (e.g. an unauthenticated
    /// page carrying sensitive data that must never be indexed or leak a referrer). Uses <c>set</c> rather
    /// than <c>init</c>, matching <see cref="TrustedProxyOptions"/>'s list properties, for reliable
    /// configuration binding.
    /// </summary>
    public IReadOnlyList<SecurityHeadersPathOverride> PathOverrides { get; set; } = [];
}

/// <summary>A per-path override of a subset of <see cref="SecurityHeadersOptions"/>. See <see cref="SecurityHeadersOptions.PathOverrides"/>.</summary>
public sealed class SecurityHeadersPathOverride
{
    /// <summary>Path prefix to match via <see cref="PathString.StartsWithSegments(string)"/> (e.g. <c>"/med"</c>).</summary>
    public string PathPrefix { get; init; } = string.Empty;

    /// <summary>Overrides <see cref="SecurityHeadersOptions.ReferrerPolicy"/> for matching requests. Null keeps the default.</summary>
    public string? ReferrerPolicy { get; init; }

    /// <summary>Overrides <see cref="SecurityHeadersOptions.RobotsTag"/> for matching requests. Null keeps the default.</summary>
    public string? RobotsTag { get; init; }
}
