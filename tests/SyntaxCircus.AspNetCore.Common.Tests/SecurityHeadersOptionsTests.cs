namespace SyntaxCircus.AspNetCore.Common.Tests;

public class SecurityHeadersOptionsTests
{
    [Fact]
    public void Defaults_AreExpected()
    {
        var options = new SecurityHeadersOptions();

        options.ReferrerPolicy.ShouldBe("strict-origin-when-cross-origin");
        options.FrameOptions.ShouldBe("DENY");
        options.ContentTypeOptions.ShouldBe("nosniff");
        options.PermissionsPolicy.ShouldBe("camera=(), geolocation=(), microphone=()");
        options.ContentSecurityPolicy.ShouldBe("base-uri 'self'; form-action 'self'; frame-ancestors 'none'; upgrade-insecure-requests");
        options.StrictTransportSecurity.ShouldBe("max-age=31536000; includeSubDomains");
    }

    [Fact]
    public void SectionName_IsSecurityHeaders()
    {
        SecurityHeadersOptions.SectionName.ShouldBe("SecurityHeaders");
    }
}
