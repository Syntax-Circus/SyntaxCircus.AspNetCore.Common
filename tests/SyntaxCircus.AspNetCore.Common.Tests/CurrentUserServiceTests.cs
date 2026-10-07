namespace SyntaxCircus.AspNetCore.Common.Tests;

public class CurrentUserServiceTests
{
    [Fact]
    public void NullHttpContext_IsAnonymousUnauthenticated()
    {
        var accessor = Substitute.For<IHttpContextAccessor>();
        accessor.HttpContext.Returns((HttpContext?)null);

        var service = new CurrentUserService(accessor);

        service.IsAuthenticated.ShouldBeFalse();
        service.UserId.ShouldBeNull();
        service.Email.ShouldBeNull();
        service.DisplayName.ShouldBeNull();
        service.Principal.Identity.ShouldNotBeNull();
        service.Principal.Identity!.IsAuthenticated.ShouldBeFalse();
    }

    [Fact]
    public void AuthenticatedPrincipal_ReflectsClaims()
    {
        var identity = new ClaimsIdentity(
        [
            new Claim("sub", "user-1"),
            new Claim("email", "user@example.com"),
            new Claim("name", "Test User"),
        ], "TestAuth");
        var context = new DefaultHttpContext { User = new ClaimsPrincipal(identity) };

        var accessor = Substitute.For<IHttpContextAccessor>();
        accessor.HttpContext.Returns(context);

        var service = new CurrentUserService(accessor);

        service.IsAuthenticated.ShouldBeTrue();
        service.UserId.ShouldBe("user-1");
        service.Email.ShouldBe("user@example.com");
        service.DisplayName.ShouldBe("Test User");
    }

    [Fact]
    public void DisplayName_NoNameOrPreferredUsername_FallsBackToEmail()
    {
        var identity = new ClaimsIdentity([new Claim("email", "user@example.com")], "TestAuth");
        var context = new DefaultHttpContext { User = new ClaimsPrincipal(identity) };

        var accessor = Substitute.For<IHttpContextAccessor>();
        accessor.HttpContext.Returns(context);

        var service = new CurrentUserService(accessor);

        service.DisplayName.ShouldBe("user@example.com");
    }
}
