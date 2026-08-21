using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Routing;

namespace SyntaxCircus.AspNetCore.Common;

/// <summary>
/// Maps the static web assets required by a Blazor Web App before mapping its Razor components.
/// </summary>
public static class BlazorStaticAssetEndpointExtensions
{
    /// <summary>
    /// Maps static web assets before Razor components so framework assets such as
    /// <c>/_framework/blazor.web.js</c> are available in published applications.
    /// </summary>
    /// <typeparam name="TRootComponent">The application's root Razor component.</typeparam>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <returns>The component endpoint builder, allowing the host to select its render modes.</returns>
    public static RazorComponentsEndpointConventionBuilder MapRazorComponentsWithStaticAssets<TRootComponent>(
        this IEndpointRouteBuilder endpoints)
        where TRootComponent : IComponent
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints.MapStaticAssets();
        return endpoints.MapRazorComponents<TRootComponent>();
    }
}
