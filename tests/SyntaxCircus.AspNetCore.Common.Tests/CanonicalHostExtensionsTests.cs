namespace SyntaxCircus.AspNetCore.Common.Tests;

public class CanonicalHostExtensionsTests
{
    private static IConfiguration EmptyConfiguration()
        => new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>()).Build();

    private static IConfiguration CanonicalHostConfiguration()
        => new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["CanonicalHost:CanonicalHost"] = "www.example.com",
            ["CanonicalHost:LegacyHosts:0"] = "example.com",
        }).Build();

    [Fact]
    public void AddCanonicalHostRedirect_NullServices_ThrowsArgumentNullException()
    {
        Should.Throw<ArgumentNullException>(() =>
            CanonicalHostExtensions.AddCanonicalHostRedirect(null!, EmptyConfiguration()));
    }

    [Fact]
    public void AddCanonicalHostRedirect_NullConfiguration_ThrowsArgumentNullException()
    {
        var services = new ServiceCollection();

        Should.Throw<ArgumentNullException>(() => services.AddCanonicalHostRedirect(null!));
    }

    [Fact]
    public void UseCanonicalHostRedirect_NullApp_ThrowsArgumentNullException()
    {
        Should.Throw<ArgumentNullException>(() => CanonicalHostExtensions.UseCanonicalHostRedirect(null!));
    }

    [Fact]
    public async Task UseCanonicalHostRedirect_LegacyHost_RedirectsPreservingPathAndQuery()
    {
        using var server = TestServerFactory.Create(
            services => services.AddCanonicalHostRedirect(CanonicalHostConfiguration()),
            app =>
            {
                app.UseCanonicalHostRedirect();
                app.MapGet("/profile", () => "ok");
            });
        using var client = server.CreateClient();

        var request = new HttpRequestMessage(HttpMethod.Get, "/profile?id=5")
        {
            Headers = { Host = "example.com" },
        };
        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(System.Net.HttpStatusCode.MovedPermanently);
        response.Headers.Location.ShouldNotBeNull();
        response.Headers.Location!.ToString().ShouldBe("http://www.example.com/profile?id=5");
    }

    [Fact]
    public async Task UseCanonicalHostRedirect_NonLegacyHost_PassesThrough()
    {
        using var server = TestServerFactory.Create(
            services => services.AddCanonicalHostRedirect(CanonicalHostConfiguration()),
            app =>
            {
                app.UseCanonicalHostRedirect();
                app.MapGet("/profile", () => "ok");
            });
        using var client = server.CreateClient();

        var request = new HttpRequestMessage(HttpMethod.Get, "/profile")
        {
            Headers = { Host = "www.example.com" },
        };
        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(System.Net.HttpStatusCode.OK);
    }

    [Fact]
    public async Task UseCanonicalHostRedirect_CanonicalHostUnset_IsNoOp()
    {
        using var server = TestServerFactory.Create(
            services => services.AddCanonicalHostRedirect(EmptyConfiguration()),
            app =>
            {
                app.UseCanonicalHostRedirect();
                app.MapGet("/profile", () => "ok");
            });
        using var client = server.CreateClient();

        var request = new HttpRequestMessage(HttpMethod.Get, "/profile")
        {
            Headers = { Host = "example.com" },
        };
        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(System.Net.HttpStatusCode.OK);
    }

    [Fact]
    public async Task UseCanonicalHostRedirect_ForceHttps_UpgradesSchemeInSameRedirect()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["CanonicalHost:CanonicalHost"] = "www.example.com",
            ["CanonicalHost:LegacyHosts:0"] = "example.com",
            ["CanonicalHost:ForceHttps"] = "true",
        }).Build();

        using var server = TestServerFactory.Create(
            services => services.AddCanonicalHostRedirect(configuration),
            app =>
            {
                app.UseCanonicalHostRedirect();
                app.MapGet("/profile", () => "ok");
            });
        using var client = server.CreateClient();

        var request = new HttpRequestMessage(HttpMethod.Get, "/profile")
        {
            Headers = { Host = "example.com" },
        };
        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        response.Headers.Location!.ToString().ShouldBe("https://www.example.com/profile");
    }

    [Fact]
    public async Task UseCanonicalHostRedirect_PermanentFalse_Returns302()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["CanonicalHost:CanonicalHost"] = "www.example.com",
            ["CanonicalHost:LegacyHosts:0"] = "example.com",
            ["CanonicalHost:Permanent"] = "false",
        }).Build();

        using var server = TestServerFactory.Create(
            services => services.AddCanonicalHostRedirect(configuration),
            app =>
            {
                app.UseCanonicalHostRedirect();
                app.MapGet("/profile", () => "ok");
            });
        using var client = server.CreateClient();

        var request = new HttpRequestMessage(HttpMethod.Get, "/profile")
        {
            Headers = { Host = "example.com" },
        };
        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(System.Net.HttpStatusCode.Found);
    }
}
