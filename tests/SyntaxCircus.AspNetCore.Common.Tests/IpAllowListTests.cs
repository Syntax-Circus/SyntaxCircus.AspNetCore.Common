namespace SyntaxCircus.AspNetCore.Common.Tests;

public class IpAllowListTests
{
    private static IpAllowList AllowList(IReadOnlyList<string>? ips = null, IReadOnlyList<string>? networks = null) =>
        new(Options.Create(new IpAllowListOptions { Ips = ips ?? [], Networks = networks ?? [] }));

    [Fact]
    public void Contains_ExactIpMatch_ReturnsTrue() =>
        AllowList(ips: ["10.0.0.5"]).Contains(IPAddress.Parse("10.0.0.5")).ShouldBeTrue();

    [Fact]
    public void Contains_IpNotListed_ReturnsFalse() =>
        AllowList(ips: ["10.0.0.5"]).Contains(IPAddress.Parse("10.0.0.6")).ShouldBeFalse();

    [Fact]
    public void Contains_IpInsideCidrNetwork_ReturnsTrue() =>
        AllowList(networks: ["10.0.0.0/24"]).Contains(IPAddress.Parse("10.0.0.200")).ShouldBeTrue();

    [Fact]
    public void Contains_IpOutsideCidrNetwork_ReturnsFalse() =>
        AllowList(networks: ["10.0.0.0/24"]).Contains(IPAddress.Parse("10.0.1.1")).ShouldBeFalse();

    [Fact]
    public void Contains_NullIp_ReturnsFalse() =>
        AllowList(ips: ["10.0.0.5"]).Contains(null).ShouldBeFalse();

    [Fact]
    public void Contains_EmptyAllowList_ReturnsFalse() =>
        AllowList().Contains(IPAddress.Parse("1.2.3.4")).ShouldBeFalse();

    [Fact]
    public void Contains_MalformedEntries_AreIgnoredNotThrown() =>
        AllowList(ips: ["not-an-ip"], networks: ["not-a-network", "10.0.0.0/999"]).Contains(IPAddress.Parse("1.2.3.4")).ShouldBeFalse();
}
