using Microsoft.AspNetCore.Mvc;

namespace SyntaxCircus.AspNetCore.Common;

/// <summary>
/// Catches unhandled exceptions and writes them as an RFC 7807 <c>ProblemDetails</c> response,
/// using <see cref="ProblemDetailsMappingOptions.ExceptionMapper"/> to pick the status code and a
/// stable error code, and <see cref="ProblemDetailsMappingOptions.BuildTypeUri"/> for the
/// <c>Type</c> field.
/// </summary>
public sealed class ProblemDetailsExceptionMiddleware(
    RequestDelegate next,
    IOptions<ProblemDetailsMappingOptions> options,
    ILogger<ProblemDetailsExceptionMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        try
        {
            await next(context).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            var mapped = options.Value.ExceptionMapper(ex);
            logger.LogError(ex, "Unhandled exception mapped to problem code {ErrorCode}", mapped.ErrorCode);

            if (context.Response.HasStarted)
            {
                throw;
            }

            context.Response.Clear();
            context.Response.StatusCode = mapped.StatusCode;
            context.Response.ContentType = "application/problem+json";

            var problemDetails = new ProblemDetails
            {
                Status = mapped.StatusCode,
                Type = options.Value.BuildTypeUri(mapped.ErrorCode),
                Detail = mapped.Detail ?? ex.Message,
                Instance = context.Request.Path,
            };

            await context.Response.WriteAsJsonAsync(problemDetails).ConfigureAwait(false);
        }
    }
}
