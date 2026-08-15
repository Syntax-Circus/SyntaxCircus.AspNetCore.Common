namespace SyntaxCircus.AspNetCore.Common;

public sealed class TrustedProxyOptions
{
    public const string SectionName = "TrustedProxy";

    /// <summary>Individual proxy IP addresses to trust forwarded headers from (e.g. "10.0.0.5").</summary>
    public IReadOnlyList<string> TrustedProxies { get; set; } = [];

    /// <summary>CIDR networks to trust forwarded headers from (e.g. "10.0.0.0/8").</summary>
    public IReadOnlyList<string> TrustedNetworks { get; set; } = [];

    /// <summary>
    /// When true (the default), <see cref="TrustedProxyValidation.ValidateTrustedProxyConfiguration"/>
    /// throws at startup outside Development if neither list is configured.
    /// </summary>
    public bool RequireTrustedProxiesInProduction { get; set; } = true;
}
