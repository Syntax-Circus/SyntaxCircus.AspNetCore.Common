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

    /// <summary>
    /// Renders <paramref name="result"/> via <paramref name="onSuccess"/> on success, or as a <c>ProblemDetails</c>
    /// at the result's exact <see cref="ApiResult.StatusCode"/> on failure.
    /// </summary>
    /// <remarks>
    /// This overload is selected by <paramref name="result"/>'s compile-time type. A value produced as an
    /// <see cref="ApiResult"/> but held in a <see cref="Result"/>-typed variable binds to the plain
    /// <see cref="Result"/> overload instead, which silently discards the passthrough status code and falls
    /// back to the default kind-based mapping. Declare the return type as <see cref="ApiResult"/> wherever the
    /// passthrough status must survive.
    /// </remarks>
    public static IActionResult ToActionResult(
        this ApiResult result,
        ControllerBase controller,
        Func<IActionResult> onSuccess)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(controller);
        ArgumentNullException.ThrowIfNull(onSuccess);

        var mapper = GetMapper(controller);
        return result.IsSuccess ? onSuccess() : mapper.MapWithStatus(controller, result.Errors[0], (int)result.StatusCode!.Value);
    }

    /// <summary>
    /// Renders <paramref name="result"/> via <paramref name="onSuccess"/> on success, or as a <c>ProblemDetails</c>
    /// at the result's exact <see cref="ApiResult{T}.StatusCode"/> on failure.
    /// </summary>
    /// <remarks>
    /// This overload is selected by <paramref name="result"/>'s compile-time type. A value produced as an
    /// <see cref="ApiResult{T}"/> but held in a <see cref="Result{T}"/>-typed variable binds to the plain
    /// <see cref="Result{T}"/> overload instead, which silently discards the passthrough status code and falls
    /// back to the default kind-based mapping. Declare the return type as <see cref="ApiResult{T}"/> wherever the
    /// passthrough status must survive.
    /// </remarks>
    public static IActionResult ToActionResult<T>(
        this ApiResult<T> result,
        ControllerBase controller,
        Func<T, IActionResult> onSuccess)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(controller);
        ArgumentNullException.ThrowIfNull(onSuccess);

        var mapper = GetMapper(controller);
        return result.IsSuccess ? onSuccess(result.Value) : mapper.MapWithStatus(controller, result.Errors[0], (int)result.StatusCode!.Value);
    }

    private static ResultProblemDetailsMapper GetMapper(ControllerBase controller) =>
        controller.HttpContext.RequestServices.GetService<ResultProblemDetailsMapper>()
        ?? throw new InvalidOperationException(
            $"Result problem details services are not registered. Call {nameof(ResultProblemDetailsServiceCollectionExtensions.AddResultProblemDetails)} during service configuration.");
}
