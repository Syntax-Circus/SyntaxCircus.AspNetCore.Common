using SyntaxCircus.Common;

namespace SyntaxCircus.AspNetCore.Common;

public sealed class ResultProblemDetailsOptions
{
    public string BaseTypeUri { get; set; } = string.Empty;

    public string ValidationErrorCode { get; set; } = "validation-failed";

    public string ValidationTitle { get; set; } = "One or more validation errors occurred.";

    public Func<ResultErrorKind, int> StatusCodeMapper { get; set; } = DefaultStatusCodeMapper;

    public string BuildTypeUri(string errorCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(errorCode);

        return string.IsNullOrWhiteSpace(BaseTypeUri)
            ? errorCode
            : $"{BaseTypeUri.TrimEnd('/')}/{errorCode}";
    }

    private static int DefaultStatusCodeMapper(ResultErrorKind kind) => kind switch
    {
        ResultErrorKind.Validation => StatusCodes.Status400BadRequest,
        ResultErrorKind.Unauthenticated => StatusCodes.Status401Unauthorized,
        ResultErrorKind.Forbidden => StatusCodes.Status403Forbidden,
        ResultErrorKind.NotFound => StatusCodes.Status404NotFound,
        ResultErrorKind.Conflict => StatusCodes.Status409Conflict,
        ResultErrorKind.Failure => StatusCodes.Status500InternalServerError,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "The result error kind is not defined."),
    };
}
