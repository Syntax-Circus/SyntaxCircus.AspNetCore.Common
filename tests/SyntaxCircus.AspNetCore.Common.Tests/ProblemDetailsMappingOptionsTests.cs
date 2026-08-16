namespace SyntaxCircus.AspNetCore.Common.Tests;

public class ProblemDetailsMappingOptionsTests
{
    [Fact]
    public void BuildTypeUri_EmptyBaseTypeUri_ReturnsBareErrorCode()
    {
        var options = new ProblemDetailsMappingOptions();

        options.BuildTypeUri("not-found").ShouldBe("not-found");
    }

    [Fact]
    public void BuildTypeUri_BaseTypeUriWithTrailingSlash_Trimmed()
    {
        var options = new ProblemDetailsMappingOptions { BaseTypeUri = "https://errors.example.com/" };

        options.BuildTypeUri("not-found").ShouldBe("https://errors.example.com/not-found");
    }

    [Fact]
    public void BuildTypeUri_BaseTypeUriWithoutTrailingSlash_Combined()
    {
        var options = new ProblemDetailsMappingOptions { BaseTypeUri = "https://errors.example.com" };

        options.BuildTypeUri("not-found").ShouldBe("https://errors.example.com/not-found");
    }

    [Theory]
    [InlineData(typeof(ArgumentException), 400, "bad-request")]
    [InlineData(typeof(UnauthorizedAccessException), 403, "forbidden")]
    [InlineData(typeof(KeyNotFoundException), 404, "not-found")]
    [InlineData(typeof(InvalidOperationException), 409, "conflict")]
    [InlineData(typeof(NotImplementedException), 500, "internal-error")]
    public void DefaultMapper_MapsExceptionTypeToExpectedStatusAndCode(Type exceptionType, int expectedStatus, string expectedCode)
    {
        var options = new ProblemDetailsMappingOptions();
        var exception = (Exception)Activator.CreateInstance(exceptionType)!;

        var mapping = options.ExceptionMapper(exception);

        mapping.StatusCode.ShouldBe(expectedStatus);
        mapping.ErrorCode.ShouldBe(expectedCode);
    }
}
