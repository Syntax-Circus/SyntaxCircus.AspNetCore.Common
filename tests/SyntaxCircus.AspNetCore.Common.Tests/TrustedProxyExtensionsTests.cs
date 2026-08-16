namespace SyntaxCircus.AspNetCore.Common.Tests;

public class TrustedProxyExtensionsTests
{
    private static IHostEnvironment FakeEnvironment(string environmentName)
    {
        var environment = Substitute.For<IHostEnvironment>();
        environment.EnvironmentName.Returns(environmentName);
        return environment;
    }

    [Fact]
    public void ValidateTrustedProxyConfiguration_NullEnvironment_ThrowsArgumentNullException()
    {
        Should.Throw<ArgumentNullException>(() =>
            TrustedProxyExtensions.ValidateTrustedProxyConfiguration(null!, new TrustedProxyOptions()));
    }

    [Fact]
    public void ValidateTrustedProxyConfiguration_NullOptions_ThrowsArgumentNullException()
    {
        Should.Throw<ArgumentNullException>(() =>
            FakeEnvironment("Production").ValidateTrustedProxyConfiguration(null!));
    }

    [Fact]
    public void ValidateTrustedProxyConfiguration_RequireFalse_NoThrowRegardlessOfConfig()
    {
        var options = new TrustedProxyOptions { RequireTrustedProxiesInProduction = false };

        Should.NotThrow(() => FakeEnvironment("Production").ValidateTrustedProxyConfiguration(options));
    }

    [Fact]
    public void ValidateTrustedProxyConfiguration_Development_NoThrowEvenWithoutConfig()
    {
        var options = new TrustedProxyOptions();

        Should.NotThrow(() => FakeEnvironment("Development").ValidateTrustedProxyConfiguration(options));
    }

    [Fact]
    public void ValidateTrustedProxyConfiguration_NonDevelopmentNoProxiesOrNetworks_Throws()
    {
        var options = new TrustedProxyOptions();

        Should.Throw<InvalidOperationException>(() => FakeEnvironment("Production").ValidateTrustedProxyConfiguration(options));
    }

    [Fact]
    public void ValidateTrustedProxyConfiguration_TrustedProxiesConfigured_NoThrow()
    {
        var options = new TrustedProxyOptions { TrustedProxies = ["10.0.0.5"] };

        Should.NotThrow(() => FakeEnvironment("Production").ValidateTrustedProxyConfiguration(options));
    }

    [Fact]
    public void ValidateTrustedProxyConfiguration_TrustedNetworksConfigured_NoThrow()
    {
        var options = new TrustedProxyOptions { TrustedNetworks = ["10.0.0.0/8"] };

        Should.NotThrow(() => FakeEnvironment("Production").ValidateTrustedProxyConfiguration(options));
    }

    [Fact]
    public void AddTrustedProxyForwardedHeaders_NullServices_ThrowsArgumentNullException()
    {
        Should.Throw<ArgumentNullException>(() =>
            TrustedProxyExtensions.AddTrustedProxyForwardedHeaders(null!, new ConfigurationBuilder().Build()));
    }

    [Fact]
    public void AddTrustedProxyForwardedHeaders_NullConfiguration_ThrowsArgumentNullException()
    {
        var services = new ServiceCollection();

        Should.Throw<ArgumentNullException>(() => services.AddTrustedProxyForwardedHeaders(null!));
    }

    [Fact]
    public void AddTrustedProxyForwardedHeaders_ValidProxyIp_AddedToKnownProxies()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["TrustedProxy:TrustedProxies:0"] = "10.0.0.5",
        }).Build();
        var services = new ServiceCollection();
        services.AddTrustedProxyForwardedHeaders(configuration);

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<ForwardedHeadersOptions>>().Value;

        options.KnownProxies.ShouldContain(System.Net.IPAddress.Parse("10.0.0.5"));
    }

    [Fact]
    public void AddTrustedProxyForwardedHeaders_UnparseableProxyIp_SilentlySkipped()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["TrustedProxy:TrustedProxies:0"] = "not-an-ip",
        }).Build();
        var services = new ServiceCollection();
        services.AddTrustedProxyForwardedHeaders(configuration);

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<ForwardedHeadersOptions>>().Value;

        options.KnownProxies.ShouldBeEmpty();
    }

    [Fact]
    public void AddTrustedProxyForwardedHeaders_ValidNetwork_AddedToKnownIPNetworks()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["TrustedProxy:TrustedNetworks:0"] = "10.0.0.0/8",
        }).Build();
        var services = new ServiceCollection();
        services.AddTrustedProxyForwardedHeaders(configuration);

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<ForwardedHeadersOptions>>().Value;

        options.KnownIPNetworks.Count.ShouldBe(1);
    }

    [Theory]
    [InlineData("no-slash")]
    [InlineData("10.0.0.0/not-a-number")]
    [InlineData("not-an-ip/8")]
    public void AddTrustedProxyForwardedHeaders_MalformedNetwork_SilentlySkipped(string malformedNetwork)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["TrustedProxy:TrustedNetworks:0"] = malformedNetwork,
        }).Build();
        var services = new ServiceCollection();
        services.AddTrustedProxyForwardedHeaders(configuration);

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<ForwardedHeadersOptions>>().Value;

        options.KnownIPNetworks.ShouldBeEmpty();
    }
}
