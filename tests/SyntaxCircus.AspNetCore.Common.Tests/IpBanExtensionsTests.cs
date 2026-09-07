namespace SyntaxCircus.AspNetCore.Common.Tests;

public sealed class IpBanExtensionsTests
{
    [Fact]
    public void AddIpBanTracking_NullServices_ThrowsArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() =>
            IpBanExtensions.AddIpBanTracking(null!, new ConfigurationBuilder().Build()));

    [Fact]
    public void AddIpBanTracking_NullConfiguration_ThrowsArgumentNullException()
    {
        var services = new ServiceCollection();

        Should.Throw<ArgumentNullException>(() => services.AddIpBanTracking(null!));
    }

    [Fact]
    public void UseIpBanTracking_NullApp_ThrowsArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => IpBanExtensions.UseIpBanTracking(null!));

    [Fact]
    public void AddIpBanTracking_BindsOptionsFromConfiguredSection()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["IpBan:RejectionThreshold"] = "5",
            ["IpBan:WindowMinutes"] = "2",
            ["IpBan:BanDurationHours"] = "1",
        }).Build();
        var services = new ServiceCollection();
        services.AddIpBanTracking(configuration);

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<IpBanOptions>>().Value;

        options.RejectionThreshold.ShouldBe(5);
        options.WindowMinutes.ShouldBe(2);
        options.BanDurationHours.ShouldBe(1);
    }

    [Fact]
    public void AddIpBanTracking_RegistersIpBanTrackerAsSingleton()
    {
        var services = new ServiceCollection();
        services.AddIpBanTracking(new ConfigurationBuilder().Build());

        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<IpBanTracker>().ShouldBeSameAs(provider.GetRequiredService<IpBanTracker>());
    }

    [Fact]
    public void AddIpBanTracking_DoesNotOverrideAnAlreadyRegisteredTimeProvider()
    {
        var fakeClock = new FakeTimeProvider();
        var services = new ServiceCollection();
        services.AddSingleton<TimeProvider>(fakeClock);
        services.AddIpBanTracking(new ConfigurationBuilder().Build());

        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<TimeProvider>().ShouldBeSameAs(fakeClock);
    }

    // TestServer leaves Connection.RemoteIpAddress null, so these simulate a real caller IP via a
    // fixed-IP middleware ahead of UseIpBanTracking() - otherwise IsBanned's null-IP no-op would make
    // both "banned" and "unbanned" scenarios indistinguishable.
    private static readonly IPAddress CallerIp = IPAddress.Parse("203.0.113.7");

    private static void UseFixedCallerIp(IApplicationBuilder app) =>
        app.Use(async (context, next) => { context.Connection.RemoteIpAddress = CallerIp; await next(); });

    [Fact]
    public async Task AddIpBanTracking_And_UseIpBanTracking_BannedIpGetsForbidden()
    {
        using var server = TestServerFactory.Create(
            services => services.AddIpBanTracking(new ConfigurationBuilder().Build()),
            app =>
            {
                UseFixedCallerIp(app);
                app.UseIpBanTracking();
                app.UseRouting();
                app.MapGet("/", () => "ok");
            });
        var tracker = server.Services.GetRequiredService<IpBanTracker>();
        tracker.Ban(CallerIp, DateTimeOffset.UtcNow.AddHours(1));
        using var client = server.CreateClient();

        var response = await client.GetAsync(new Uri("/", UriKind.Relative), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task UseIpBanTracking_UnbannedIp_PassesThrough()
    {
        using var server = TestServerFactory.Create(
            services => services.AddIpBanTracking(new ConfigurationBuilder().Build()),
            app =>
            {
                UseFixedCallerIp(app);
                app.UseIpBanTracking();
                app.UseRouting();
                app.MapGet("/", () => "ok");
            });
        using var client = server.CreateClient();

        var response = await client.GetAsync(new Uri("/", UriKind.Relative), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    private sealed class FakeTimeProvider : TimeProvider;
}
