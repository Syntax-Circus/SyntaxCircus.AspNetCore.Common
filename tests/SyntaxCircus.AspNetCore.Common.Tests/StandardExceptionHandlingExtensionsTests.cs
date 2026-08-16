namespace SyntaxCircus.AspNetCore.Common.Tests;

public class StandardExceptionHandlingExtensionsTests
{
    private static ApplicationBuilder CreateApp(string environmentName)
    {
        var environment = Substitute.For<IHostEnvironment>();
        environment.EnvironmentName.Returns(environmentName);
        var services = new ServiceCollection();
        services.AddSingleton(environment);
        return new ApplicationBuilder(services.BuildServiceProvider());
    }

    [Fact]
    public void UseStandardExceptionHandling_NullApp_ThrowsArgumentNullException()
    {
        Should.Throw<ArgumentNullException>(() => StandardExceptionHandlingExtensions.UseStandardExceptionHandling(null!));
    }

    [Fact]
    public void UseStandardExceptionHandling_Development_SkipsAndReturnsSameBuilder()
    {
        var app = CreateApp("Development");

        var result = app.UseStandardExceptionHandling();

        result.ShouldBeSameAs(app);
    }

    [Fact]
    public void UseStandardExceptionHandling_NonDevelopment_ReturnsSameBuilder()
    {
        var app = CreateApp("Production");

        var result = app.UseStandardExceptionHandling();

        result.ShouldBeSameAs(app);
    }
}
