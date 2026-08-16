namespace SyntaxCircus.AspNetCore.Common.Tests;

public class ProblemDetailsExceptionExtensionsTests
{
    [Fact]
    public void AddProblemDetailsExceptionHandling_NullServices_ThrowsArgumentNullException()
    {
        Should.Throw<ArgumentNullException>(() => ProblemDetailsExceptionExtensions.AddProblemDetailsExceptionHandling(null!));
    }

    [Fact]
    public void AddProblemDetailsExceptionHandling_ConfigureCallback_Applied()
    {
        var services = new ServiceCollection();
        services.AddProblemDetailsExceptionHandling(options => options.BaseTypeUri = "https://errors.example.com");

        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<IOptions<ProblemDetailsMappingOptions>>().Value.BaseTypeUri.ShouldBe("https://errors.example.com");
    }

    [Fact]
    public void UseProblemDetailsExceptionHandling_NullApp_ThrowsArgumentNullException()
    {
        Should.Throw<ArgumentNullException>(() => ProblemDetailsExceptionExtensions.UseProblemDetailsExceptionHandling(null!));
    }

    [Fact]
    public void UseProblemDetailsExceptionHandling_ReturnsSameApplicationBuilder()
    {
        var services = new ServiceCollection();
        services.AddProblemDetailsExceptionHandling();
        services.AddLogging();
        var app = new ApplicationBuilder(services.BuildServiceProvider());

        var result = app.UseProblemDetailsExceptionHandling();

        result.ShouldBeSameAs(app);
    }
}
