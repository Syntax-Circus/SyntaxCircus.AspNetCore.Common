namespace SyntaxCircus.AspNetCore.Common;

public sealed record ProblemMapping(int StatusCode, string ErrorCode, string? Detail = null);

/// <summary>
/// Drives <see cref="ProblemDetailsExceptionMiddleware"/>: a base URI used to build stable,
/// machine-readable <c>ProblemDetails.Type</c> values (<c>{BaseTypeUri}/{errorCode}</c>), and the
/// exception → status/code mapping itself. The mapping is deliberately a delegate you configure —
/// which exception types mean what HTTP status in your product is not something a shared package
/// can decide for you.
/// </summary>
public sealed class ProblemDetailsMappingOptions
{
    public const string SectionName = "ProblemDetails";

    public string BaseTypeUri { get; set; } = string.Empty;

    public Func<Exception, ProblemMapping> ExceptionMapper { get; set; } = DefaultMapper;

    public string BuildTypeUri(string errorCode) =>
        string.IsNullOrWhiteSpace(BaseTypeUri) ? errorCode : $"{BaseTypeUri.TrimEnd('/')}/{errorCode}";

    private static ProblemMapping DefaultMapper(Exception exception) => exception switch
    {
        ArgumentException => new ProblemMapping(StatusCodes.Status400BadRequest, "bad-request"),
        UnauthorizedAccessException => new ProblemMapping(StatusCodes.Status403Forbidden, "forbidden"),
        KeyNotFoundException => new ProblemMapping(StatusCodes.Status404NotFound, "not-found"),
        InvalidOperationException => new ProblemMapping(StatusCodes.Status409Conflict, "conflict"),
        _ => new ProblemMapping(StatusCodes.Status500InternalServerError, "internal-error"),
    };
}
