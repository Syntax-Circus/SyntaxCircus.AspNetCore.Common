namespace SyntaxCircus.AspNetCore.Common;

public static class ProblemDetailsExceptionExtensions
{
    public static IServiceCollection AddProblemDetailsExceptionHandling(
        this IServiceCollection services,
        Action<ProblemDetailsMappingOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var optionsBuilder = services.AddOptions<ProblemDetailsMappingOptions>();
        if (configure is not null)
        {
            optionsBuilder.Configure(configure);
        }

        return services;
    }

    public static IApplicationBuilder UseProblemDetailsExceptionHandling(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        return app.UseMiddleware<ProblemDetailsExceptionMiddleware>();
    }
}
