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
    [InlineData(typeof(ArgumentException), 400, "bad-request", "The request was invalid.")]
    [InlineData(typeof(UnauthorizedAccessException), 403, "forbidden", "You do not have permission to perform this action.")]
    [InlineData(typeof(KeyNotFoundException), 404, "not-found", "The requested resource was not found.")]
    [InlineData(typeof(InvalidOperationException), 409, "conflict", "The request could not be completed due to a conflict.")]
    [InlineData(typeof(NotImplementedException), 500, "internal-error", "An unexpected error occurred.")]
    public void DefaultMapper_MapsExceptionTypeToExpectedStatusCodeAndDetail(Type exceptionType, int expectedStatus, string expectedCode, string expectedDetail)
    {
        var options = new ProblemDetailsMappingOptions();
        var exception = (Exception)Activator.CreateInstance(exceptionType)!;

        var mapping = options.ExceptionMapper(exception);

        mapping.StatusCode.ShouldBe(expectedStatus);
        mapping.ErrorCode.ShouldBe(expectedCode);
        mapping.Detail.ShouldBe(expectedDetail);
    }

    [Fact]
    public void IncludeExceptionMessageInDetail_DefaultsToFalse()
    {
        var options = new ProblemDetailsMappingOptions();

        options.IncludeExceptionMessageInDetail.ShouldBeFalse();
    }
}
