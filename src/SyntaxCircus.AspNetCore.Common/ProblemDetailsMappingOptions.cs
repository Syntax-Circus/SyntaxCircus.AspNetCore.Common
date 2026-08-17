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

    /// <summary>
    /// When <see langword="true"/>, <see cref="ProblemDetailsExceptionMiddleware"/> falls back to the
    /// raw <see cref="Exception.Message"/> for <c>ProblemDetails.Detail</c> whenever <see cref="ExceptionMapper"/>
    /// returns a <see langword="null"/> <see cref="ProblemMapping.Detail"/>. Defaults to <see langword="false"/>:
    /// exception messages can carry internals (SQL fragments, file paths, connection strings) that
    /// shouldn't reach an API client by default. Only opt in if you've verified your exception messages
    /// are safe to expose to clients.
    /// </summary>
    public bool IncludeExceptionMessageInDetail { get; set; }

    public string BuildTypeUri(string errorCode) =>
        string.IsNullOrWhiteSpace(BaseTypeUri) ? errorCode : $"{BaseTypeUri.TrimEnd('/')}/{errorCode}";

    private static ProblemMapping DefaultMapper(Exception exception) => exception switch
    {
        ArgumentException => new ProblemMapping(StatusCodes.Status400BadRequest, "bad-request", "The request was invalid."),
        UnauthorizedAccessException => new ProblemMapping(StatusCodes.Status403Forbidden, "forbidden", "You do not have permission to perform this action."),
        KeyNotFoundException => new ProblemMapping(StatusCodes.Status404NotFound, "not-found", "The requested resource was not found."),
        InvalidOperationException => new ProblemMapping(StatusCodes.Status409Conflict, "conflict", "The request could not be completed due to a conflict."),
        _ => new ProblemMapping(StatusCodes.Status500InternalServerError, "internal-error", "An unexpected error occurred."),
    };
}
