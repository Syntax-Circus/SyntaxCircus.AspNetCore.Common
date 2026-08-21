using Microsoft.AspNetCore.Components;

namespace SyntaxCircus.AspNetCore.Common.Tests;

public sealed class BlazorStaticAssetEndpointExtensionsTests
{
    [Fact]
    public void MapRazorComponentsWithStaticAssets_NullEndpoints_Throws()
    {
        var exception = Should.Throw<ArgumentNullException>(() =>
            ((IEndpointRouteBuilder)null!).MapRazorComponentsWithStaticAssets<TestRootComponent>());

        exception.ParamName.ShouldBe("endpoints");
    }

    private sealed class TestRootComponent : ComponentBase;
}
