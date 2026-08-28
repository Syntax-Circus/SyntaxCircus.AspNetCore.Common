namespace SyntaxCircus.AspNetCore.Common;

public static class ResultProblemDetailsServiceCollectionExtensions
{
    public static IServiceCollection AddResultProblemDetails(
        this IServiceCollection services,
        Action<ResultProblemDetailsOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var optionsBuilder = services.AddOptions<ResultProblemDetailsOptions>();
        if (configure is not null)
        {
            optionsBuilder.Configure(configure);
        }

        services.AddSingleton<ResultProblemDetailsMapper>();
        return services;
    }
}
