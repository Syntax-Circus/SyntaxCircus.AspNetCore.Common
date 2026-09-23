namespace SyntaxCircus.AspNetCore.Common.Tests;

public sealed class ForwardedClientIpHttpClientBuilderExtensionsTests
{
    [Fact]
    public void AddForwardedClientIp_NullBuilder_ThrowsArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() =>
            ForwardedClientIpHttpClientBuilderExtensions.AddForwardedClientIp(null!));

    [Fact]
    public void AddForwardedClientIp_RegistersHttpContextAccessor()
    {
        var services = new ServiceCollection();
        services.AddHttpClient("api").AddForwardedClientIp();

        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<IHttpContextAccessor>().ShouldNotBeNull();
    }

    [Fact]
    public async Task AddForwardedClientIp_WiresHandlerIntoNamedClientPipeline()
    {
        var inner = new CapturingHandler();
        var services = new ServiceCollection();
        services.AddHttpClient("api")
            .AddForwardedClientIp()
            .ConfigurePrimaryHttpMessageHandler(() => inner);

        using var provider = services.BuildServiceProvider();

        var accessor = provider.GetRequiredService<IHttpContextAccessor>();
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse("203.0.113.7");
        accessor.HttpContext = context;

        var factory = provider.GetRequiredService<IHttpClientFactory>();
        using var client = factory.CreateClient("api");
        client.BaseAddress = new Uri("https://api.example.com");

        using var request = new HttpRequestMessage(HttpMethod.Get, "/health");
        await client.SendAsync(request, TestContext.Current.CancellationToken);

        inner.LastRequest!.Headers.GetValues("X-Forwarded-For").ShouldBe(["203.0.113.7"]);
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
