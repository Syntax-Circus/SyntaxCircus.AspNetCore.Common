namespace SyntaxCircus.AspNetCore.Common;

/// <summary>
/// IPs/networks exempt from rate-limit tracking and <see cref="IpBanTracker"/> bans — for internal
/// services that legitimately call at high volume. See <see cref="IpAllowList"/>.
/// </summary>
public sealed class IpAllowListOptions
{
    public const string SectionName = "IpAllowList";

    /// <summary>Individual IPs exempt from rejection tracking and bans (e.g. "10.0.0.5").</summary>
    public IReadOnlyList<string> Ips { get; init; } = [];

    /// <summary>CIDR networks exempt from rejection tracking and bans (e.g. "10.0.0.0/24").</summary>
    public IReadOnlyList<string> Networks { get; init; } = [];
}
