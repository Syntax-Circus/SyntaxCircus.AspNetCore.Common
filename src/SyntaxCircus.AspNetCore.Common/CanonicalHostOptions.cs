namespace SyntaxCircus.AspNetCore.Common;

public sealed class CanonicalHostOptions
{
    public const string SectionName = "CanonicalHost";

    /// <summary>Target hostname (no scheme/port). Null (the default) disables the redirect entirely.</summary>
    public string? CanonicalHost { get; set; }

    /// <summary>Hostnames that get redirected to <see cref="CanonicalHost"/>, preserving path/query.</summary>
    public IReadOnlyList<string> LegacyHosts { get; set; } = [];

    /// <summary>
    /// When true, the redirect also upgrades http to https for matching legacy hosts, collapsing what would
    /// otherwise be two redirects (this middleware, then <c>UseHsts</c>/<c>UseHttpsRedirection</c>) into one.
    /// Does not affect requests already on the canonical host — that scheme upgrade is a separate, orthogonal
    /// concern still owned by <c>UseHsts</c>/<c>UseHttpsRedirection</c>. Default false (preserves scheme).
    /// </summary>
    public bool ForceHttps { get; set; }

    /// <summary>
    /// Whether the redirect is permanent (301, the default) or temporary (302). Set false for a staged
    /// rollout — verify the redirect behaves as expected with a reversible 302 before flipping to permanent.
    /// </summary>
    public bool Permanent { get; set; } = true;
}
