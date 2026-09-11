using System.Net;

namespace SyntaxCircus.AspNetCore.Common;

/// <summary>
/// A shared, config-driven exemption list of IPs/CIDR networks — e.g. internal services that must
/// never be rate limited or banned. <see cref="IpBanTracker"/> consults this internally, and an app
/// can also resolve it from DI to build its own rate-limiter <c>isExempt</c> predicate, so one config
/// section covers both concerns without listing an internal IP twice.
/// </summary>
/// <remarks>
/// Registered as a singleton by <see cref="IpAllowListExtensions.AddIpAllowList"/> (and automatically
/// by <see cref="IpBanExtensions.AddIpBanTracking"/>). Entries are parsed once, lazily; a malformed
/// entry is ignored rather than throwing.
/// </remarks>
public sealed class IpAllowList(IOptions<IpAllowListOptions> options)
{
    private readonly Lazy<(HashSet<IPAddress> Ips, IPNetwork[] Networks)> parsed = new(() => Parse(options.Value));

    /// <summary>True if <paramref name="ip"/> matches a configured exempt IP or falls within a configured exempt network.</summary>
    public bool Contains(IPAddress? ip)
    {
        if (ip is null)
        {
            return false;
        }

        var (ips, networks) = parsed.Value;
        if (ips.Contains(ip))
        {
            return true;
        }

        foreach (var network in networks)
        {
            if (network.Contains(ip))
            {
                return true;
            }
        }

        return false;
    }

    private static (HashSet<IPAddress>, IPNetwork[]) Parse(IpAllowListOptions options)
    {
        var ips = new HashSet<IPAddress>();
        foreach (var entry in options.Ips)
        {
            if (IPAddress.TryParse(entry, out var address))
            {
                ips.Add(address);
            }
        }

        var networks = new List<IPNetwork>();
        foreach (var entry in options.Networks)
        {
            var parts = entry.Split('/');
            if (parts.Length == 2
                && IPAddress.TryParse(parts[0], out var networkAddress)
                && int.TryParse(parts[1], out var prefixLength)
                && prefixLength >= 0
                && prefixLength <= (networkAddress.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6 ? 128 : 32))
            {
                networks.Add(new IPNetwork(networkAddress, prefixLength));
            }
        }

        return (ips, [.. networks]);
    }
}
