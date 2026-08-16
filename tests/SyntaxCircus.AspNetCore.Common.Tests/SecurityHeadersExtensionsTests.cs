namespace SyntaxCircus.AspNetCore.Common.Tests;

public class SecurityHeadersExtensionsTests
{
    private static IConfiguration EmptyConfiguration()
        => new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>()).Build();

    [Fact]
    public void AddSecurityHeaders_NullServices_ThrowsArgumentNullException()
    {
        Should.Throw<ArgumentNullException>(() =>
            SecurityHeadersExtensions.AddSecurityHeaders(null!, EmptyConfiguration()));
    }

    [Fact]
    public void AddSecurityHeaders_NullConfiguration_ThrowsArgumentNullException()
    {
        var services = new ServiceCollection();

        Should.Throw<ArgumentNullException>(() => services.AddSecurityHeaders(null!));
    }

    [Fact]
    public void UseSecurityHeaders_NullApp_ThrowsArgumentNullException()
    {
        Should.Throw<ArgumentNullException>(() => SecurityHeadersExtensions.UseSecurityHeaders(null!));
    }

    [Fact]
    public async Task UseSecurityHeaders_SetsAllHeadersFromOptions()
    {
        using var server = TestServerFactory.Create(
            services => services.AddSecurityHeaders(EmptyConfiguration()),
            app =>
            {
                app.UseSecurityHeaders();
                app.MapGet("/", () => "ok");
            });
        using var client = server.CreateClient();

        var response = await client.GetAsync(new Uri("/", UriKind.Relative), TestContext.Current.CancellationToken);

        response.Headers.GetValues("Referrer-Policy").ShouldContain("strict-origin-when-cross-origin");
        response.Headers.GetValues("X-Frame-Options").ShouldContain("DENY");
        response.Headers.GetValues("X-Content-Type-Options").ShouldContain("nosniff");
        response.Headers.GetValues("Permissions-Policy").ShouldContain("camera=(), geolocation=(), microphone=()");
        response.Headers.GetValues("Content-Security-Policy").ShouldNotBeEmpty();
        response.Headers.GetValues("Strict-Transport-Security").ShouldContain("max-age=31536000; includeSubDomains");
    }
}
