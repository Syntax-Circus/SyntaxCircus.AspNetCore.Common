namespace SyntaxCircus.AspNetCore.Common.Tests;

public sealed class IpBanMiddlewareTests
{
    private static IpBanTracker Tracker() =>
        new(Options.Create(new IpBanOptions()), new IpAllowList(Options.Create(new IpAllowListOptions())));

    [Fact]
    public async Task InvokeAsync_BannedIp_ShortCircuitsWithForbidden()
    {
        var tracker = Tracker();
        var ip = IPAddress.Parse("1.2.3.4");
        tracker.Ban(ip, DateTimeOffset.UtcNow.AddHours(1));
        var context = new DefaultHttpContext(); context.Connection.RemoteIpAddress = ip;
        var nextCalled = false;
        var middleware = new IpBanMiddleware(_ => { nextCalled = true; return Task.CompletedTask; });

        await middleware.InvokeAsync(context, tracker, TimeProvider.System);

        context.Response.StatusCode.ShouldBe(StatusCodes.Status403Forbidden);
        nextCalled.ShouldBeFalse();
    }

    [Fact]
    public async Task InvokeAsync_UnbannedIp_CallsNext()
    {
        var tracker = Tracker();
        var context = new DefaultHttpContext(); context.Connection.RemoteIpAddress = IPAddress.Parse("1.2.3.4");
        var nextCalled = false;
        var middleware = new IpBanMiddleware(_ => { nextCalled = true; return Task.CompletedTask; });

        await middleware.InvokeAsync(context, tracker, TimeProvider.System);

        nextCalled.ShouldBeTrue();
        context.Response.StatusCode.ShouldBe(StatusCodes.Status200OK);
    }
}
