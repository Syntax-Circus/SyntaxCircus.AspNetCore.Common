using Microsoft.AspNetCore.Mvc;
using SyntaxCircus.Common;

namespace SyntaxCircus.AspNetCore.Common;

public static class ResultActionResultExtensions
{
    public static IActionResult ToActionResult(
        this Result result,
        ControllerBase controller,
        Func<IActionResult> onSuccess)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(controller);
        ArgumentNullException.ThrowIfNull(onSuccess);

        var mapper = GetMapper(controller);
        return result.IsSuccess ? onSuccess() : mapper.Map(controller, result.Errors);
    }

    public static IActionResult ToActionResult<T>(
        this Result<T> result,
        ControllerBase controller,
        Func<T, IActionResult> onSuccess)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(controller);
        ArgumentNullException.ThrowIfNull(onSuccess);

        var mapper = GetMapper(controller);
        return result.IsSuccess ? onSuccess(result.Value) : mapper.Map(controller, result.Errors);
    }

    private static ResultProblemDetailsMapper GetMapper(ControllerBase controller) =>
        controller.HttpContext.RequestServices.GetService<ResultProblemDetailsMapper>()
        ?? throw new InvalidOperationException(
            $"Result problem details services are not registered. Call {nameof(ResultProblemDetailsServiceCollectionExtensions.AddResultProblemDetails)} during service configuration.");
}
