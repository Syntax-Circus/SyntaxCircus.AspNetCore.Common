namespace SyntaxCircus.AspNetCore.Common;

public static class SearchIndexingHeaderExtensions
{
    private static readonly string[] DefaultExcludedPaths = ["/robots.txt", "/sitemap.xml"];

    public static IServiceCollection AddSearchIndexing(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.Configure<SearchIndexingOptions>(configuration.GetSection(SearchIndexingOptions.SectionName));
        return services;
    }

    public static IApplicationBuilder UseSearchIndexingHeaders(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        return app.UseRobotsTagHeader(context =>
        {
            var options = context.RequestServices.GetRequiredService<IOptions<SearchIndexingOptions>>().Value;
            var excluded = DefaultExcludedPaths.Concat(options.ExcludedPaths)
                .Any(path => context.Request.Path.Equals(path, StringComparison.OrdinalIgnoreCase));

            return options.BlockPageMetadata && !excluded ? options.RobotsDirective : null;
        });
    }

    public static IApplicationBuilder UseSearchIndexingHeaders(
        this IApplicationBuilder app,
        Func<HttpContext, bool> shouldApply)
    {
        ArgumentNullException.ThrowIfNull(app);
        ArgumentNullException.ThrowIfNull(shouldApply);

        return app.UseRobotsTagHeader(context => shouldApply(context) ? SearchIndexingOptions.NoIndexDirective : null);
    }

    private static IApplicationBuilder UseRobotsTagHeader(this IApplicationBuilder app, Func<HttpContext, string?> valueSelector)
    {
        return app.Use(async (context, next) =>
            {
                context.Response.OnStarting(() =>
                    {
                        var value = valueSelector(context);
                        if (!string.IsNullOrEmpty(value)
                            && !context.Response.Headers.ContainsKey("X-Robots-Tag"))
                        {
                            context.Response.Headers["X-Robots-Tag"] = value;
                        }

                        return Task.CompletedTask;
                    });

                await next();
            });
    }
}
