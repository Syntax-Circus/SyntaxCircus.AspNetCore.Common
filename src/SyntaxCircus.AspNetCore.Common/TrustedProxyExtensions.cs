using System.Net;
using Microsoft.AspNetCore.HttpOverrides;

namespace SyntaxCircus.AspNetCore.Common;

public static class TrustedProxyExtensions
{
    /// <summary>
    /// Fails fast at startup if forwarded-header trust is misconfigured for a non-Development
    /// environment: no trusted proxies/networks configured while
    /// <see cref="TrustedProxyOptions.RequireTrustedProxiesInProduction"/> is true. Catches the
    /// mistake of deploying behind a reverse proxy without telling ASP.NET Core which upstream
    /// hosts to actually trust — without this, forwarded headers are trusted from anyone.
    /// </summary>
    public static void ValidateTrustedProxyConfiguration(this IHostEnvironment environment, TrustedProxyOptions options)
    {
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(options);

        if (!options.RequireTrustedProxiesInProduction || environment.IsDevelopment())
        {
            return;
        }

        if (options.TrustedProxies.Count == 0 && options.TrustedNetworks.Count == 0)
        {
            throw new InvalidOperationException(
                $"{TrustedProxyOptions.SectionName} has no TrustedProxies or TrustedNetworks configured. " +
                "Forwarded headers (X-Forwarded-For/X-Forwarded-Proto) would be trusted from any source, which " +
                "is unsafe behind a reverse proxy. Configure trusted proxies/networks, or set " +
                "RequireTrustedProxiesInProduction to false if this host isn't behind a reverse proxy.");
        }
    }

    /// <summary>Binds <see cref="TrustedProxyOptions"/> from configuration and wires <see cref="ForwardedHeadersOptions"/> from it.</summary>
    public static IServiceCollection AddTrustedProxyForwardedHeaders(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var options = new TrustedProxyOptions();
        configuration.GetSection(TrustedProxyOptions.SectionName).Bind(options);

        services.Configure<ForwardedHeadersOptions>(forwardedHeadersOptions =>
        {
            forwardedHeadersOptions.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            forwardedHeadersOptions.KnownProxies.Clear();
            forwardedHeadersOptions.KnownIPNetworks.Clear();

            foreach (var proxy in options.TrustedProxies)
            {
                if (IPAddress.TryParse(proxy, out var address))
                {
                    forwardedHeadersOptions.KnownProxies.Add(address);
                }
            }

            foreach (var network in options.TrustedNetworks)
            {
                var parts = network.Split('/');
                if (parts.Length == 2
                    && IPAddress.TryParse(parts[0], out var networkAddress)
                    && int.TryParse(parts[1], out var prefixLength))
                {
                    forwardedHeadersOptions.KnownIPNetworks.Add(new System.Net.IPNetwork(networkAddress, prefixLength));
                }
            }
        });

        return services;
    }
}
