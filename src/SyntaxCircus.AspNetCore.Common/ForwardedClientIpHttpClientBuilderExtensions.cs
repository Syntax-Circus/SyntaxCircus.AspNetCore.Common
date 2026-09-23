using Microsoft.Extensions.DependencyInjection.Extensions;

namespace SyntaxCircus.AspNetCore.Common;

public static class ForwardedClientIpHttpClientBuilderExtensions
{
    /// <summary>
    /// Wires <see cref="ForwardedClientIpHandler"/> into this <see cref="IHttpClientBuilder"/>'s
    /// outgoing pipeline: registers <see cref="IHttpContextAccessor"/> and the handler itself (both
    /// idempotently, so this is safe to call for more than one client), then adds the handler as an
    /// <c>AddHttpMessageHandler</c> entry. See <see cref="ForwardedClientIpHandler"/> for what it does
    /// and when it's safe to use.
    /// </summary>
    public static IHttpClientBuilder AddForwardedClientIp(this IHttpClientBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.AddHttpContextAccessor();
        builder.Services.TryAddTransient<ForwardedClientIpHandler>();
        builder.AddHttpMessageHandler<ForwardedClientIpHandler>();

        return builder;
    }
}
