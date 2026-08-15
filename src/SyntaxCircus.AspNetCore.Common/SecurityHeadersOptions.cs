namespace SyntaxCircus.AspNetCore.Common;

public sealed class SecurityHeadersOptions
{
    public const string SectionName = "SecurityHeaders";

    public string ReferrerPolicy { get; init; } = "strict-origin-when-cross-origin";

    public string FrameOptions { get; init; } = "DENY";

    public string ContentTypeOptions { get; init; } = "nosniff";

    public string PermissionsPolicy { get; init; } = "camera=(), geolocation=(), microphone=()";

    public string ContentSecurityPolicy { get; init; } =
        "base-uri 'self'; form-action 'self'; frame-ancestors 'none'; upgrade-insecure-requests";

    public string StrictTransportSecurity { get; init; } = "max-age=31536000; includeSubDomains";
}
