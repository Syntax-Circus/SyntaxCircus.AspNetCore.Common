using Microsoft.Extensions.DependencyInjection.Extensions;

namespace SyntaxCircus.AspNetCore.Common;

public static class IpAllowListExtensions
{
    /// <summary>
    /// Binds <see cref="IpAllowListOptions"/> from configuration and registers <see cref="IpAllowList"/>
    /// as a singleton. Safe to call more than once (e.g. directly and via <see cref="IpBanExtensions.AddIpBanTracking"/>) —
    /// registration is idempotent.
    /// </summary>
    public static IServiceCollection AddIpAllowList(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.Configure<IpAllowListOptions>(configuration.GetSection(IpAllowListOptions.SectionName));
        services.TryAddSingleton<IpAllowList>();

        return services;
    }
}
