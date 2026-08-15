namespace SyntaxCircus.AspNetCore.Common;

public static class SecurityHeadersExtensions
{
    public static IServiceCollection AddSecurityHeaders(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.Configure<SecurityHeadersOptions>(configuration.GetSection(SecurityHeadersOptions.SectionName));
        return services;
    }

    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        return app.Use(async (context, next) =>
        {
            context.Response.OnStarting(() =>
            {
                var options = context.RequestServices.GetRequiredService<IOptions<SecurityHeadersOptions>>().Value;
                var headers = context.Response.Headers;

                headers["Referrer-Policy"] = options.ReferrerPolicy;
                headers["X-Frame-Options"] = options.FrameOptions;
                headers["X-Content-Type-Options"] = options.ContentTypeOptions;
                headers["Permissions-Policy"] = options.PermissionsPolicy;
                headers["Content-Security-Policy"] = options.ContentSecurityPolicy;
                headers["Strict-Transport-Security"] = options.StrictTransportSecurity;

                return Task.CompletedTask;
            });

            await next();
        });
    }
}
