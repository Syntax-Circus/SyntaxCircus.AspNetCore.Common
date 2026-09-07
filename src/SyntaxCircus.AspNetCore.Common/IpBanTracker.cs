using System.Collections.Concurrent;
using System.Net;

namespace SyntaxCircus.AspNetCore.Common;

/// <summary>
/// Tracks per-IP rate-limit rejections in a rolling window and bans an IP outright once
/// <see cref="IpBanOptions.RejectionThreshold"/> is crossed within <see cref="IpBanOptions.WindowMinutes"/>.
/// Pair with <see cref="IpBanMiddleware"/>, which enforces the ban this class records.
/// </summary>
/// <remarks>
/// State is in-memory only (a <see cref="ConcurrentDictionary{TKey,TValue}"/> per instance) and this type is
/// registered as a singleton by <see cref="IpBanExtensions.AddIpBanTracking"/> — safe as long as the host
/// runs as a single instance. Move to shared/distributed state (e.g. Redis) before scaling horizontally,
/// or bans and rejection counts won't be seen consistently across instances.
/// </remarks>
public sealed class IpBanTracker(IOptions<IpBanOptions> options)
{
    private readonly ConcurrentDictionary<string, DateTimeOffset> _bans = new();
    private readonly ConcurrentDictionary<string, (int Count, DateTimeOffset WindowStart)> _rejections = new();

    /// <summary>True if <paramref name="ip"/> is currently under an active ban as of <paramref name="now"/>.</summary>
    public bool IsBanned(IPAddress? ip, DateTimeOffset now) =>
        ip is not null && _bans.TryGetValue(ip.ToString(), out var until) && until > now;

    /// <summary>
    /// Records one rejection for <paramref name="ip"/> and returns true exactly once, the moment the
    /// rejection count within the current window first reaches <see cref="IpBanOptions.RejectionThreshold"/> —
    /// callers should treat that single true result as the signal to call <see cref="Ban"/>.
    /// </summary>
    public bool RecordRejection(IPAddress? ip, DateTimeOffset now, out int countInWindow)
    {
        countInWindow = 0;
        if (ip is null) return false;
        var window = TimeSpan.FromMinutes(options.Value.WindowMinutes);
        var state = _rejections.AddOrUpdate(ip.ToString(),
            _ => (1, now),
            (_, existing) => now - existing.WindowStart > window ? (1, now) : (existing.Count + 1, existing.WindowStart));
        countInWindow = state.Count;
        return state.Count == options.Value.RejectionThreshold;
    }

    /// <summary>Bans <paramref name="ip"/> until <paramref name="until"/>.</summary>
    public void Ban(IPAddress ip, DateTimeOffset until) => _bans[ip.ToString()] = until;
}
