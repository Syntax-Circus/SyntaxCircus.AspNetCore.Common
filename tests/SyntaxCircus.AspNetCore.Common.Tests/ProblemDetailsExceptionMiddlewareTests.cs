namespace SyntaxCircus.AspNetCore.Common.Tests;

public class ProblemDetailsExceptionMiddlewareTests
{
    private static DefaultHttpContext CreateContext()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        return context;
    }

    private static ProblemDetailsExceptionMiddleware CreateMiddleware(
        RequestDelegate next,
        ProblemDetailsMappingOptions? options = null)
        => new(next, Options.Create(options ?? new ProblemDetailsMappingOptions()), Substitute.For<ILogger<ProblemDetailsExceptionMiddleware>>());

    [Fact]
    public async Task InvokeAsync_NullContext_ThrowsArgumentNullException()
    {
        var middleware = CreateMiddleware(_ => Task.CompletedTask);

        await Should.ThrowAsync<ArgumentNullException>(() => middleware.InvokeAsync(null!));
    }

    [Fact]
    public async Task InvokeAsync_NextCompletesCleanly_PassesThrough()
    {
        var context = CreateContext();
        var middleware = CreateMiddleware(_ => Task.CompletedTask);

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.ShouldBe(200);
    }

    [Fact]
    public async Task InvokeAsync_NextThrowsOperationCanceled_NotCaught()
    {
        var context = CreateContext();
        var middleware = CreateMiddleware(_ => throw new OperationCanceledException());

        await Should.ThrowAsync<OperationCanceledException>(() => middleware.InvokeAsync(context));
    }

    [Fact]
    public async Task InvokeAsync_NextThrowsMappedException_WritesProblemDetailsJson()
    {
        var context = CreateContext();
        context.Request.Path = "/api/thing";
        var middleware = CreateMiddleware(_ => throw new KeyNotFoundException("missing"));

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.ShouldBe(404);
        context.Response.ContentType.ShouldBe("application/problem+json");

        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body);
        var body = await reader.ReadToEndAsync(TestContext.Current.CancellationToken);
        body.ShouldContain("\"status\":404");
        body.ShouldContain("/api/thing");
    }

    [Fact]
    public async Task InvokeAsync_DetailFallsBackToExceptionMessage_WhenMapperDetailNull()
    {
        var context = CreateContext();
        var middleware = CreateMiddleware(_ => throw new ArgumentException("specific detail message"));

        await middleware.InvokeAsync(context);

        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body);
        var body = await reader.ReadToEndAsync(TestContext.Current.CancellationToken);
        body.ShouldContain("specific detail message");
    }

    [Fact]
    public async Task InvokeAsync_ResponseAlreadyStarted_Rethrows()
    {
        using var server = TestServerFactory.Create(
            services => services.AddProblemDetailsExceptionHandling(),
            app =>
            {
                app.UseProblemDetailsExceptionHandling();
                app.MapGet("/", async ctx =>
                {
                    await ctx.Response.StartAsync(TestContext.Current.CancellationToken);
                    throw new InvalidOperationException("late failure");
                });
            });
        using var client = server.CreateClient();

        await Should.ThrowAsync<Exception>(() => client.GetAsync(new Uri("/", UriKind.Relative), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task InvokeAsync_CustomExceptionMapper_Used()
    {
        var context = CreateContext();
        var options = new ProblemDetailsMappingOptions
        {
            ExceptionMapper = _ => new ProblemMapping(418, "im-a-teapot", "custom detail"),
        };
        var middleware = CreateMiddleware(_ => throw new InvalidOperationException("ignored"), options);

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.ShouldBe(418);
    }
}
