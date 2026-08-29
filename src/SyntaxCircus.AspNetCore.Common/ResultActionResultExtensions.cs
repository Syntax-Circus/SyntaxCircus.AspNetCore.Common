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

        if (result is ApiResult apiResult)
        {
            return apiResult.ToActionResult(controller, onSuccess);
        }

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

        if (result is ApiResult<T> apiResult)
        {
            return apiResult.ToActionResult(controller, onSuccess);
        }

        var mapper = GetMapper(controller);
        return result.IsSuccess ? onSuccess(result.Value) : mapper.Map(controller, result.Errors);
    }

    /// <summary>
    /// Renders <paramref name="result"/> via <paramref name="onSuccess"/> on success, or as a <c>ProblemDetails</c>
    /// at the result's exact <see cref="ApiResult.StatusCode"/> on failure.
    /// </summary>
    /// <remarks>
    /// The base <see cref="Result"/>/<see cref="Result{T}"/> overloads also detect an <see cref="ApiResult"/>
    /// instance at runtime and delegate here, so a value declared as <see cref="Result"/> still renders its
    /// exact passthrough status even when the compile-time type doesn't say <see cref="ApiResult"/>.
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
    /// The base <see cref="Result"/>/<see cref="Result{T}"/> overloads also detect an <see cref="ApiResult{T}"/>
    /// instance at runtime and delegate here, so a value declared as <see cref="Result{T}"/> still renders its
    /// exact passthrough status even when the compile-time type doesn't say <see cref="ApiResult{T}"/>.
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
