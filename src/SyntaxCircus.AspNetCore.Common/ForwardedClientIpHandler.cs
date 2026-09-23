namespace SyntaxCircus.AspNetCore.Common;

/// <summary>
/// The sending half of <see cref="TrustedProxyExtensions"/>: stamps every outbound request with the
/// current visitor's IP in <c>X-Forwarded-For</c>, replacing anything already there. Intended for
/// server-to-server calls a host makes to its own backend over a private network (e.g. a Blazor/web
/// front end calling its own API) — without this, every visitor's request arrives at the downstream
/// API carrying the calling host's own address as the connecting peer, collapsing per-IP rate
/// limiting and auto-ban into a single shared bucket for all of the caller's traffic.
/// </summary>
/// <remarks>
/// <para>
/// This only takes effect once the downstream API is configured to trust the calling host's
/// address/network as a proxy (<see cref="TrustedProxyOptions"/>) — otherwise the header is ignored
/// and the downstream API falls back to its own connection IP, same as before.
/// </para>
/// <para>
/// <see cref="HttpContext.Connection"/>'s <c>RemoteIpAddress</c> must already be the real visitor IP
/// for this to be meaningful, which means the calling host itself needs to already be running
/// <c>UseForwardedHeaders()</c> behind its own trusted reverse proxy — otherwise this forwards that
/// proxy's IP, not the visitor's.
/// </para>
/// <para>
/// Only attach this handler to <see cref="HttpClient"/>s that call your own trusted backends: it
/// discloses the visitor's IP address to whatever the request's target is.
/// </para>
/// <para>
/// No-op when there's no current <see cref="HttpContext"/> (background work, some interactive-circuit
/// calls) or no resolved remote IP.
/// </para>
/// </remarks>
public sealed class ForwardedClientIpHandler(IHttpContextAccessor httpContextAccessor) : DelegatingHandler
{
    private const string XForwardedForHeader = "X-Forwarded-For";

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var remoteIp = httpContextAccessor.HttpContext?.Connection.RemoteIpAddress;
        if (remoteIp is not null)
        {
            // Kestrel can report an IPv4 client as an IPv4-mapped IPv6 address (e.g.
            // "::ffff:192.168.1.10") depending on how it's bound. Map it down to the plain
            // dotted-quad form so the header matches what a downstream IP allowlist/CIDR check
            // expects -- otherwise the same visitor would appear under two different string
            // representations of the same address.
            if (remoteIp.IsIPv4MappedToIPv6)
            {
                remoteIp = remoteIp.MapToIPv4();
            }

            request.Headers.Remove(XForwardedForHeader);
            request.Headers.TryAddWithoutValidation(XForwardedForHeader, remoteIp.ToString());
        }

        return base.SendAsync(request, cancellationToken);
    }
}
