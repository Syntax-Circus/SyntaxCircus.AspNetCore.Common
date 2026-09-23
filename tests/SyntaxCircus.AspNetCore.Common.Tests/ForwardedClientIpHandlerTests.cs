namespace SyntaxCircus.AspNetCore.Common.Tests;

public sealed class ForwardedClientIpHandlerTests
{
    [Fact]
    public async Task SendAsync_WithRemoteIp_SetsForwardedForHeaderFromContext()
    {
        var inner = new CapturingHandler();
        var accessor = new HttpContextAccessor { HttpContext = ContextWithRemoteIp("203.0.113.7") };

        await SendAsync(accessor, inner);

        inner.LastRequest!.Headers.GetValues("X-Forwarded-For").ShouldBe(["203.0.113.7"]);
    }

    [Fact]
    public async Task SendAsync_WithExistingForwardedForHeader_ReplacesItRatherThanAppending()
    {
        var inner = new CapturingHandler();
        var accessor = new HttpContextAccessor { HttpContext = ContextWithRemoteIp("203.0.113.7") };

        await SendAsync(accessor, inner, request => request.Headers.Add("X-Forwarded-For", "198.51.100.1"));

        inner.LastRequest!.Headers.GetValues("X-Forwarded-For").ShouldBe(["203.0.113.7"]);
    }

    [Fact]
    public async Task SendAsync_NoHttpContext_IsNoOp()
    {
        var inner = new CapturingHandler();
        var accessor = new HttpContextAccessor(); // HttpContext stays null (background call).

        await SendAsync(accessor, inner);

        inner.LastRequest!.Headers.Contains("X-Forwarded-For").ShouldBeFalse();
    }

    [Fact]
    public async Task SendAsync_NoRemoteIpAddress_IsNoOp()
    {
        var inner = new CapturingHandler();
        var accessor = new HttpContextAccessor { HttpContext = new DefaultHttpContext() }; // RemoteIpAddress null.

        await SendAsync(accessor, inner);

        inner.LastRequest!.Headers.Contains("X-Forwarded-For").ShouldBeFalse();
    }

    [Fact]
    public async Task SendAsync_NoHttpContext_LeavesExistingForwardedForHeaderAlone()
    {
        var inner = new CapturingHandler();
        var accessor = new HttpContextAccessor();

        await SendAsync(accessor, inner, request => request.Headers.Add("X-Forwarded-For", "198.51.100.1"));

        inner.LastRequest!.Headers.GetValues("X-Forwarded-For").ShouldBe(["198.51.100.1"]);
    }

    [Fact]
    public async Task SendAsync_Ipv4MappedToIPv6RemoteIp_IsNormalizedToPlainIPv4()
    {
        var inner = new CapturingHandler();
        var mapped = IPAddress.Parse("::ffff:203.0.113.9");
        mapped.IsIPv4MappedToIPv6.ShouldBeTrue(); // sanity check on the fixture itself.
        var accessor = new HttpContextAccessor { HttpContext = ContextWithRemoteIp(mapped) };

        await SendAsync(accessor, inner);

        inner.LastRequest!.Headers.GetValues("X-Forwarded-For").ShouldBe(["203.0.113.9"]);
    }

    private static async Task SendAsync(
        HttpContextAccessor accessor,
        HttpMessageHandler inner,
        Action<HttpRequestMessage>? configureRequest = null)
    {
        var handler = new ForwardedClientIpHandler(accessor) { InnerHandler = inner };
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://api.example.com") };

        using var request = new HttpRequestMessage(HttpMethod.Get, "/health");
        configureRequest?.Invoke(request);

        await client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private static DefaultHttpContext ContextWithRemoteIp(string ip) => ContextWithRemoteIp(IPAddress.Parse(ip));

    private static DefaultHttpContext ContextWithRemoteIp(IPAddress ip)
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = ip;
        return context;
    }

    private sealed class CapturingHandler : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }
    }
}
