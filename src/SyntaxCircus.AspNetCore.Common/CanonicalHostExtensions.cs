namespace SyntaxCircus.AspNetCore.Common;

public static class CanonicalHostExtensions
{
    public static IServiceCollection AddCanonicalHostRedirect(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.Configure<CanonicalHostOptions>(configuration.GetSection(CanonicalHostOptions.SectionName));
        return services;
    }

    public static IApplicationBuilder UseCanonicalHostRedirect(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        return app.Use(async (context, next) =>
        {
            var options = context.RequestServices.GetRequiredService<IOptions<CanonicalHostOptions>>().Value;

            if (!string.IsNullOrEmpty(options.CanonicalHost)
                && options.LegacyHosts.Contains(context.Request.Host.Host, StringComparer.OrdinalIgnoreCase))
            {
                var scheme = options.ForceHttps ? "https" : context.Request.Scheme;
                var redirectUri = new UriBuilder(scheme, options.CanonicalHost)
                {
                    Path = context.Request.Path.Value,
                    Query = context.Request.QueryString.Value,
                }.Uri;

                context.Response.Redirect(redirectUri.ToString(), permanent: options.Permanent);
                return;
            }

            await next();
        });
    }
}
