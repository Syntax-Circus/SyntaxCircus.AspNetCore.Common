namespace SyntaxCircus.AspNetCore.Common.Tests;

public class CurrentUserServiceExtensionsTests
{
    [Fact]
    public void AddCurrentUserService_NullServices_ThrowsArgumentNullException()
    {
        Should.Throw<ArgumentNullException>(() => CurrentUserServiceExtensions.AddCurrentUserService(null!));
    }

    [Fact]
    public void AddCurrentUserService_RegistersIHttpContextAccessor()
    {
        var services = new ServiceCollection();
        services.AddCurrentUserService();

        using var provider = services.BuildServiceProvider();

        provider.GetService<IHttpContextAccessor>().ShouldNotBeNull();
    }

    [Fact]
    public void AddCurrentUserService_ResolvesAsCurrentUserService()
    {
        var services = new ServiceCollection();
        services.AddCurrentUserService();

        using var scope = services.BuildServiceProvider().CreateScope();

        scope.ServiceProvider.GetRequiredService<ICurrentUserService>().ShouldBeOfType<CurrentUserService>();
    }

    [Fact]
    public void AddCurrentUserService_IsScoped_SameScopeReturnsSameInstance()
    {
        var services = new ServiceCollection();
        services.AddCurrentUserService();

        using var scope = services.BuildServiceProvider().CreateScope();

        var first = scope.ServiceProvider.GetRequiredService<ICurrentUserService>();
        var second = scope.ServiceProvider.GetRequiredService<ICurrentUserService>();

        first.ShouldBeSameAs(second);
    }

    [Fact]
    public void AddCurrentUserService_IsScoped_DifferentScopesReturnDifferentInstances()
    {
        var services = new ServiceCollection();
        services.AddCurrentUserService();

        using var provider = services.BuildServiceProvider();
        using var scopeA = provider.CreateScope();
        using var scopeB = provider.CreateScope();

        var a = scopeA.ServiceProvider.GetRequiredService<ICurrentUserService>();
        var b = scopeB.ServiceProvider.GetRequiredService<ICurrentUserService>();

        a.ShouldNotBeSameAs(b);
    }
}
