using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using SyntaxCircus.Common;

namespace SyntaxCircus.AspNetCore.Common;

internal sealed class ResultProblemDetailsMapper(IOptions<ResultProblemDetailsOptions> options)
{
    private const string ProblemJsonContentType = "application/problem+json";
    private readonly ResultProblemDetailsOptions _options = options.Value;

    public IActionResult Map(ControllerBase controller, IReadOnlyList<ResultError> errors)
    {
        ArgumentNullException.ThrowIfNull(controller);
        ArgumentNullException.ThrowIfNull(errors);

        if (errors.Count == 0)
        {
            throw new ArgumentException("A failure result must contain at least one error.", nameof(errors));
        }

        var statusCodeMapper = _options.StatusCodeMapper
            ?? throw new InvalidOperationException($"{nameof(ResultProblemDetailsOptions.StatusCodeMapper)} cannot be null.");
        var statusCode = statusCodeMapper(errors[0].Kind);

        return errors[0].Kind == ResultErrorKind.Validation
            ? MapValidation(controller, errors, statusCode)
            : MapProblem(controller, errors[0], statusCode);
    }

    private ObjectResult MapProblem(ControllerBase controller, ResultError error, int statusCode)
    {
        var problem = new ProblemDetails
        {
            Status = statusCode,
            Type = _options.BuildTypeUri(error.Code),
            Title = ReasonPhrases.GetReasonPhrase(statusCode),
            Detail = error.Message,
            Instance = controller.HttpContext.Request.Path.Value,
        };

        return CreateObjectResult(problem, statusCode);
    }

    private ObjectResult MapValidation(
        ControllerBase controller,
        IReadOnlyList<ResultError> errors,
        int statusCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(_options.ValidationErrorCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(_options.ValidationTitle);

        var messageGroups = GroupByTarget(errors, error => error.Message);
        var codeGroups = GroupByTarget(errors, error => error.Code);
        var problem = new ValidationProblemDetails(messageGroups)
        {
            Status = statusCode,
            Type = _options.BuildTypeUri(_options.ValidationErrorCode),
            Title = _options.ValidationTitle,
            Detail = errors[0].Message,
            Instance = controller.HttpContext.Request.Path.Value,
        };
        problem.Extensions["errorCodes"] = codeGroups;

        return CreateObjectResult(problem, statusCode);
    }

    private static Dictionary<string, string[]> GroupByTarget(
        IEnumerable<ResultError> errors,
        Func<ResultError, string> selector) =>
        errors
            .GroupBy(error => error.Target ?? string.Empty, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Select(selector).ToArray(),
                StringComparer.Ordinal);

    private static ObjectResult CreateObjectResult(ProblemDetails problem, int statusCode)
    {
        var result = new ObjectResult(problem) { StatusCode = statusCode };
        result.ContentTypes.Add(ProblemJsonContentType);
        return result;
    }
}
