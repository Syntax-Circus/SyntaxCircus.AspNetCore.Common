using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace SyntaxCircus.AspNetCore.Common.Tests;

public class RateLimiterOptionsExtensionsTests
{
    [Fact]
    public void AddPerIpFixedWindow_NullOptions_ThrowsArgumentNullException()
    {
        Should.Throw<ArgumentNullException>(() =>
            RateLimiterOptionsExtensions.AddPerIpFixedWindow(null!, "policy", 1, TimeSpan.FromMinutes(1)));
    }

    [Fact]
    public void AddPerSubjectFixedWindow_NullOptions_ThrowsArgumentNullException()
    {
        Should.Throw<ArgumentNullException>(() =>
            RateLimiterOptionsExtensions.AddPerSubjectFixedWindow(null!, "policy", 1, TimeSpan.FromMinutes(1)));
    }

    [Fact]
    public void AddPartitionedFixedWindow_NullOptions_ThrowsArgumentNullException()
    {
        Should.Throw<ArgumentNullException>(() =>
            RateLimiterOptionsExtensions.AddPartitionedFixedWindow(
                null!,
                "policy",
                _ => "key",
                1,
                TimeSpan.FromMinutes(1)));
    }

    [Fact]
    public void AddPartitionedFixedWindow_NullPartitionKeySelector_ThrowsArgumentNullException()
    {
        var options = new RateLimiterOptions();
        Should.Throw<ArgumentNullException>(() =>
            options.AddPartitionedFixedWindow(
                "policy",
                null!,
                1,
                TimeSpan.FromMinutes(1)));
    }

    [Fact]
    public void UseProblemDetailsRejection_NullOptions_ThrowsArgumentNullException()
    {
        Should.Throw<ArgumentNullException>(() => RateLimiterOptionsExtensions.UseProblemDetailsRejection(null!));
    }

    [Fact]
    public async Task PerIpFixedWindow_ExceedingLimit_RejectsWithProblemDetailsJson()
    {
        using var server = TestServerFactory.Create(
            services =>
            {
                services.AddProblemDetailsExceptionHandling();
                services.AddRateLimiter(options =>
                {
                    options.AddPerIpFixedWindow("test-policy", permitLimit: 2, window: TimeSpan.FromMinutes(1));
                    options.UseProblemDetailsRejection();
                });
            },
            app =>
            {
                app.UseRouting();
                app.UseRateLimiter();
                app.MapGet("/limited", () => "ok").RequireRateLimiting("test-policy");
            });
        using var client = server.CreateClient();

        var first = await client.GetAsync(new Uri("/limited", UriKind.Relative), TestContext.Current.CancellationToken);
        var second = await client.GetAsync(new Uri("/limited", UriKind.Relative), TestContext.Current.CancellationToken);
        var third = await client.GetAsync(new Uri("/limited", UriKind.Relative), TestContext.Current.CancellationToken);

        first.StatusCode.ShouldBe(System.Net.HttpStatusCode.OK);
        second.StatusCode.ShouldBe(System.Net.HttpStatusCode.OK);
        third.StatusCode.ShouldBe((System.Net.HttpStatusCode)429);
        third.Content.Headers.ContentType!.MediaType.ShouldBe("application/problem+json");

        var body = await third.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.ShouldContain("\"status\":429");
    }

    [Fact]
    public async Task PerSubjectFixedWindow_DifferentAuthenticatedSubjects_HaveIndependentQuotas()
    {
        using var server = TestServerFactory.Create(
            services =>
            {
                services.AddRateLimiter(options =>
                {
                    options.AddPerSubjectFixedWindow("subject-policy", permitLimit: 1, window: TimeSpan.FromMinutes(1));
                    options.UseProblemDetailsRejection();
                });
            },
            app =>
            {
                app.Use(async (ctx, next) =>
                {
                    var userId = ctx.Request.Headers["X-Test-User"].FirstOrDefault();
                    if (!string.IsNullOrEmpty(userId))
                    {
                        var identity = new System.Security.Claims.ClaimsIdentity([new System.Security.Claims.Claim("sub", userId)], "Test");
                        ctx.User = new System.Security.Claims.ClaimsPrincipal(identity);
                    }

                    await next();
                });
                app.UseRouting();
                app.UseRateLimiter();
                app.MapGet("/limited", () => "ok").RequireRateLimiting("subject-policy");
            });
        using var client = server.CreateClient();

        client.DefaultRequestHeaders.Add("X-Test-User", "user-a");
        var userAFirst = await client.GetAsync(new Uri("/limited", UriKind.Relative), TestContext.Current.CancellationToken);
        var userASecond = await client.GetAsync(new Uri("/limited", UriKind.Relative), TestContext.Current.CancellationToken);

        client.DefaultRequestHeaders.Remove("X-Test-User");
        client.DefaultRequestHeaders.Add("X-Test-User", "user-b");
        var userBFirst = await client.GetAsync(new Uri("/limited", UriKind.Relative), TestContext.Current.CancellationToken);

        userAFirst.StatusCode.ShouldBe(System.Net.HttpStatusCode.OK);
        userASecond.StatusCode.ShouldBe((System.Net.HttpStatusCode)429);
        userBFirst.StatusCode.ShouldBe(System.Net.HttpStatusCode.OK);
    }

    [Fact]
    public async Task PartitionedFixedWindow_CustomSelector_CanPartitionBySubjectAndRoute()
    {
        using var server = TestServerFactory.Create(
            services =>
            {
                services.AddRateLimiter(options =>
                {
                    options.AddPartitionedFixedWindow(
                        "composite-policy",
                        ctx => $"{ctx.User.FindFirst("sub")?.Value ?? "anon"}:{ctx.Request.Path}",
                        permitLimit: 1,
                        window: TimeSpan.FromMinutes(1));
                    options.UseProblemDetailsRejection();
                });
            },
            app =>
            {
                app.Use(async (ctx, next) =>
                {
                    var userId = ctx.Request.Headers["X-Test-User"].FirstOrDefault();
                    if (!string.IsNullOrEmpty(userId))
                    {
                        var identity = new System.Security.Claims.ClaimsIdentity([new System.Security.Claims.Claim("sub", userId)], "Test");
                        ctx.User = new System.Security.Claims.ClaimsPrincipal(identity);
                    }

                    await next();
                });
                app.UseRouting();
                app.UseRateLimiter();
                app.MapGet("/limited/one", () => "ok").RequireRateLimiting("composite-policy");
                app.MapGet("/limited/two", () => "ok").RequireRateLimiting("composite-policy");
            });
        using var client = server.CreateClient();

        client.DefaultRequestHeaders.Add("X-Test-User", "user-a");
        var userAFirstOnRouteOne = await client.GetAsync(new Uri("/limited/one", UriKind.Relative), TestContext.Current.CancellationToken);
        var userASecondOnRouteOne = await client.GetAsync(new Uri("/limited/one", UriKind.Relative), TestContext.Current.CancellationToken);
        var userAFirstOnRouteTwo = await client.GetAsync(new Uri("/limited/two", UriKind.Relative), TestContext.Current.CancellationToken);

        client.DefaultRequestHeaders.Remove("X-Test-User");
        client.DefaultRequestHeaders.Add("X-Test-User", "user-b");
        var userBFirstOnRouteOne = await client.GetAsync(new Uri("/limited/one", UriKind.Relative), TestContext.Current.CancellationToken);

        userAFirstOnRouteOne.StatusCode.ShouldBe(System.Net.HttpStatusCode.OK);
        userASecondOnRouteOne.StatusCode.ShouldBe((System.Net.HttpStatusCode)429);
        userAFirstOnRouteTwo.StatusCode.ShouldBe(System.Net.HttpStatusCode.OK);
        userBFirstOnRouteOne.StatusCode.ShouldBe(System.Net.HttpStatusCode.OK);
    }

    [Fact]
    public async Task PerIpFixedWindow_WithConfigureOverride_UsesConfiguredPermitLimit()
    {
        using var server = TestServerFactory.Create(
            services =>
            {
                services.AddRateLimiter(options =>
                {
                    options.AddPerIpFixedWindow(
                        "configured-ip-policy",
                        permitLimit: 0,
                        window: TimeSpan.FromMinutes(1),
                        configure: fixedWindowOptions => fixedWindowOptions.PermitLimit = 1);
                    options.UseProblemDetailsRejection();
                });
            },
            app =>
            {
                app.UseRouting();
                app.UseRateLimiter();
                app.MapGet("/limited", () => "ok").RequireRateLimiting("configured-ip-policy");
            });
        using var client = server.CreateClient();

        var response = await client.GetAsync(new Uri("/limited", UriKind.Relative), TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(System.Net.HttpStatusCode.OK);
    }

    private sealed class FakeRateLimitLease(bool hasRetryAfter, TimeSpan retryAfter) : RateLimitLease
    {
        public override bool IsAcquired => false;

        public override IEnumerable<string> MetadataNames => hasRetryAfter ? [MetadataName.RetryAfter.Name] : [];

        public override bool TryGetMetadata(string metadataName, out object? metadata)
        {
            if (hasRetryAfter && metadataName == MetadataName.RetryAfter.Name)
            {
                metadata = retryAfter;
                return true;
            }

            metadata = null;
            return false;
        }
    }

    [Fact]
    public async Task UseProblemDetailsRejection_LeaseHasRetryAfterMetadata_SetsRetryAfterHeader()
    {
        var services = new ServiceCollection();
        services.AddProblemDetailsExceptionHandling();
        using var provider = services.BuildServiceProvider();

        var options = new RateLimiterOptions();
        options.UseProblemDetailsRejection();

        var context = new DefaultHttpContext { RequestServices = provider, Response = { Body = new MemoryStream() } };
        var lease = new FakeRateLimitLease(hasRetryAfter: true, retryAfter: TimeSpan.FromSeconds(30));
        var rejectedContext = new OnRejectedContext { HttpContext = context, Lease = lease };

        await options.OnRejected!(rejectedContext, TestContext.Current.CancellationToken);

        context.Response.StatusCode.ShouldBe(429);
        context.Response.Headers.RetryAfter.ToString().ShouldBe("30");
    }

    [Fact]
    public async Task UseProblemDetailsRejection_LeaseHasNoRetryAfterMetadata_OmitsHeader()
    {
        var services = new ServiceCollection();
        using var provider = services.BuildServiceProvider();

        var options = new RateLimiterOptions();
        options.UseProblemDetailsRejection();

        var context = new DefaultHttpContext { RequestServices = provider, Response = { Body = new MemoryStream() } };
        var lease = new FakeRateLimitLease(hasRetryAfter: false, retryAfter: TimeSpan.Zero);
        var rejectedContext = new OnRejectedContext { HttpContext = context, Lease = lease };

        await options.OnRejected!(rejectedContext, TestContext.Current.CancellationToken);

        context.Response.Headers.RetryAfter.ToString().ShouldBeEmpty();
    }

    [Fact]
    public async Task UseProblemDetailsRejection_NoMappingOptionsRegistered_FallsBackToBareErrorCode()
    {
        using var provider = new ServiceCollection().BuildServiceProvider();

        var options = new RateLimiterOptions();
        options.UseProblemDetailsRejection();

        var context = new DefaultHttpContext { RequestServices = provider, Response = { Body = new MemoryStream() } };
        var lease = new FakeRateLimitLease(hasRetryAfter: false, retryAfter: TimeSpan.Zero);
        var rejectedContext = new OnRejectedContext { HttpContext = context, Lease = lease };

        await options.OnRejected!(rejectedContext, TestContext.Current.CancellationToken);

        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body);
        var body = await reader.ReadToEndAsync(TestContext.Current.CancellationToken);
        body.ShouldContain("\"type\":\"rate-limited\"");
    }
}
