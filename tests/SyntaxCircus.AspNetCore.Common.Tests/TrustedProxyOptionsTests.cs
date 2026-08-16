namespace SyntaxCircus.AspNetCore.Common.Tests;

public class TrustedProxyOptionsTests
{
    [Fact]
    public void Defaults_AreExpected()
    {
        var options = new TrustedProxyOptions();

        options.TrustedProxies.ShouldBeEmpty();
        options.TrustedNetworks.ShouldBeEmpty();
        options.RequireTrustedProxiesInProduction.ShouldBeTrue();
    }

    [Fact]
    public void SectionName_IsTrustedProxy()
    {
        TrustedProxyOptions.SectionName.ShouldBe("TrustedProxy");
    }
}
