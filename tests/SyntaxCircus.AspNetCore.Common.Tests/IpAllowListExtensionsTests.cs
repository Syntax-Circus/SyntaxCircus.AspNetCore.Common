namespace SyntaxCircus.AspNetCore.Common.Tests;

public sealed class IpAllowListExtensionsTests
{
    [Fact]
    public void AddIpAllowList_NullServices_ThrowsArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() =>
            IpAllowListExtensions.AddIpAllowList(null!, new ConfigurationBuilder().Build()));

    [Fact]
    public void AddIpAllowList_NullConfiguration_ThrowsArgumentNullException()
    {
        var services = new ServiceCollection();

        Should.Throw<ArgumentNullException>(() => services.AddIpAllowList(null!));
    }

    [Fact]
    public void AddIpAllowList_BindsOptionsFromConfiguredSection()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["IpAllowList:Ips:0"] = "10.0.0.5",
            ["IpAllowList:Networks:0"] = "10.0.0.0/24",
        }).Build();
        var services = new ServiceCollection();
        services.AddIpAllowList(configuration);

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<IpAllowListOptions>>().Value;

        options.Ips.ShouldBe(["10.0.0.5"]);
        options.Networks.ShouldBe(["10.0.0.0/24"]);
    }

    [Fact]
    public void AddIpAllowList_RegistersIpAllowListAsSingleton()
    {
        var services = new ServiceCollection();
        services.AddIpAllowList(new ConfigurationBuilder().Build());

        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<IpAllowList>().ShouldBeSameAs(provider.GetRequiredService<IpAllowList>());
    }

    [Fact]
    public void AddIpAllowList_CalledTwice_StillResolvesASingleInstance()
    {
        var services = new ServiceCollection();
        services.AddIpAllowList(new ConfigurationBuilder().Build());
        services.AddIpAllowList(new ConfigurationBuilder().Build());

        using var provider = services.BuildServiceProvider();

        provider.GetServices<IpAllowList>().Count().ShouldBe(1);
    }
}
