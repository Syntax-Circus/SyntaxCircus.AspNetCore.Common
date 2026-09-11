namespace SyntaxCircus.AspNetCore.Common.Tests;

public class IpAllowListOptionsTests
{
    [Fact]
    public void Defaults_AreExpected()
    {
        var options = new IpAllowListOptions();

        options.Ips.ShouldBeEmpty();
        options.Networks.ShouldBeEmpty();
    }

    [Fact]
    public void SectionName_IsIpAllowList()
    {
        IpAllowListOptions.SectionName.ShouldBe("IpAllowList");
    }
}
