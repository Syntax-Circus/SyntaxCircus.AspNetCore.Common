using System.Globalization;
using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace SyntaxCircus.AspNetCore.Common;

public static class RateLimiterOptionsExtensions
{
    /// <summary>Adds a fixed-window policy partitioned by remote IP address.</summary>
    public static RateLimiterOptions AddPerIpFixedWindow(
        this RateLimiterOptions options,
        string policyName,
        int permitLimit,
        TimeSpan window)
    {
        ArgumentNullException.ThrowIfNull(options);

        options.AddPolicy(policyName, context => RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = permitLimit,
                Window = window,
                QueueLimit = 0,
            }));

        return options;
    }

    /// <summary>
    /// Adds a fixed-window policy partitioned by the authenticated subject (<c>sub</c> claim,
    /// falling back to <see cref="ClaimTypes.NameIdentifier"/>), or remote IP when anonymous.
    /// </summary>
    public static RateLimiterOptions AddPerSubjectFixedWindow(
        this RateLimiterOptions options,
        string policyName,
        int permitLimit,
        TimeSpan window)
    {
        ArgumentNullException.ThrowIfNull(options);

        options.AddPolicy(policyName, context =>
        {
            var partitionKey = ResolvePartitionKey(context);

            return RateLimitPartition.GetFixedWindowLimiter(
                partitionKey,
                factory: _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = permitLimit,
                    Window = window,
                    QueueLimit = 0,
                });
        });

        return options;
    }

    /// <summary>Writes a ProblemDetails 429 response (with <c>Retry-After</c> when the limiter knows it) as the rejection handler.</summary>
    public static RateLimiterOptions UseProblemDetailsRejection(this RateLimiterOptions options, string errorCode = "rate-limited")
    {
        ArgumentNullException.ThrowIfNull(options);

        options.OnRejected = async (rejectedContext, cancellationToken) =>
        {
            var response = rejectedContext.HttpContext.Response;
            response.StatusCode = StatusCodes.Status429TooManyRequests;
            response.ContentType = "application/problem+json";

            if (rejectedContext.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
            {
                response.Headers.RetryAfter = ((int)retryAfter.TotalSeconds).ToString(CultureInfo.InvariantCulture);
            }

            var mappingOptions = rejectedContext.HttpContext.RequestServices.GetService<IOptions<ProblemDetailsMappingOptions>>();
            var problemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status429TooManyRequests,
                Type = mappingOptions?.Value.BuildTypeUri(errorCode) ?? errorCode,
                Detail = "Too many requests.",
            };

            await response.WriteAsJsonAsync(problemDetails, options: null, contentType: "application/problem+json", cancellationToken: cancellationToken).ConfigureAwait(false);
        };

        return options;
    }

    private static string ResolvePartitionKey(HttpContext context)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var subject = context.User.FindFirstValue("sub") ?? context.User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!string.IsNullOrWhiteSpace(subject))
            {
                return subject;
            }
        }

        return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }
}
