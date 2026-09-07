using Microsoft.Extensions.DependencyInjection.Extensions;

namespace SyntaxCircus.AspNetCore.Common;

public static class IpBanExtensions
{
    /// <summary>
    /// Binds <see cref="IpBanOptions"/> from configuration and registers <see cref="IpBanTracker"/> as a
    /// singleton. Also registers <see cref="TimeProvider.System"/> as the default <see cref="TimeProvider"/>
    /// via <c>TryAddSingleton</c>, so this works standalone in apps that haven't already registered one —
    /// without overriding an app's own registration (e.g. a test's fake clock).
    /// </summary>
    public static IServiceCollection AddIpBanTracking(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.Configure<IpBanOptions>(configuration.GetSection(IpBanOptions.SectionName));
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<IpBanTracker>();

        return services;
    }

    /// <summary>
    /// Adds <see cref="IpBanMiddleware"/> to the pipeline. Place it early — after forwarded-header
    /// resolution (e.g. <c>UseForwardedHeaders</c>) so it sees the real client IP, and before rate
    /// limiting/authentication so a banned IP's requests don't reach either.
    /// </summary>
    public static IApplicationBuilder UseIpBanTracking(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.UseMiddleware<IpBanMiddleware>();

        return app;
    }
}
