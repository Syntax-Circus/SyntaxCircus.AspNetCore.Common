namespace SyntaxCircus.AspNetCore.Common.Tests;

public class ResultActionResultExtensionsTests
{
    [Fact]
    public void AddResultProblemDetails_ConfigureCallback_Applied()
    {
        var services = new ServiceCollection();
        services.AddResultProblemDetails(options =>
        {
            options.BaseTypeUri = "https://errors.example.com";
            options.ValidationErrorCode = "invalid-request";
            options.ValidationTitle = "Check the request.";
        });

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<ResultProblemDetailsOptions>>().Value;

        options.BaseTypeUri.ShouldBe("https://errors.example.com");
        options.ValidationErrorCode.ShouldBe("invalid-request");
        options.ValidationTitle.ShouldBe("Check the request.");
    }

    [Theory]
    [InlineData(ResultErrorKind.Validation, 400)]
    [InlineData(ResultErrorKind.Unauthenticated, 401)]
    [InlineData(ResultErrorKind.Forbidden, 403)]
    [InlineData(ResultErrorKind.NotFound, 404)]
    [InlineData(ResultErrorKind.Conflict, 409)]
    [InlineData(ResultErrorKind.Failure, 500)]
    [InlineData(ResultErrorKind.Passthrough, 500)]
    public void ToActionResult_Failure_UsesDefaultStatusMapping(ResultErrorKind kind, int expectedStatus)
    {
        var controller = CreateController();
        var result = Result.Failure(new ResultError("operation-failed", "The operation failed.", kind));

        var actionResult = result.ToActionResult(controller, () => controller.NoContent());

        GetObjectResult(actionResult).StatusCode.ShouldBe(expectedStatus);
    }

    [Fact]
    public void ToActionResult_GenericSuccess_InvokesCallbackWithValue()
    {
        var controller = CreateController();
        var result = Result<string>.Success("created");
        string? callbackValue = null;

        var actionResult = result.ToActionResult(controller, value =>
        {
            callbackValue = value;
            return controller.Created("/widgets/1", value);
        });

        callbackValue.ShouldBe("created");
        actionResult.ShouldBeOfType<CreatedResult>().Location.ShouldBe("/widgets/1");
    }

    [Fact]
    public void ToActionResult_NonGenericSuccess_InvokesCallback()
    {
        var controller = CreateController();
        var callbackInvoked = false;

        var actionResult = Result.Success().ToActionResult(controller, () =>
        {
            callbackInvoked = true;
            return controller.NoContent();
        });

        callbackInvoked.ShouldBeTrue();
        actionResult.ShouldBeOfType<NoContentResult>();
    }

    [Fact]
    public void ToActionResult_Failure_DoesNotInvokeSuccessCallback()
    {
        var controller = CreateController();
        var callbackInvoked = false;
        var result = Result<string>.Failure(new ResultError(
            "widget-not-found",
            "The widget was not found.",
            ResultErrorKind.NotFound));

        _ = result.ToActionResult(controller, value =>
        {
            callbackInvoked = true;
            return controller.Ok(value);
        });

        callbackInvoked.ShouldBeFalse();
    }

    [Fact]
    public void ToActionResult_NonValidationFailure_CreatesProblemDetails()
    {
        var controller = CreateController(options => options.BaseTypeUri = "https://errors.example.com/");
        var result = Result.Failure(new ResultError(
            "widget-not-found",
            "The widget was not found.",
            ResultErrorKind.NotFound));

        var actionResult = result.ToActionResult(controller, () => controller.NoContent());

        var objectResult = GetObjectResult(actionResult);
        var problem = objectResult.Value.ShouldBeOfType<ProblemDetails>();
        objectResult.StatusCode.ShouldBe(StatusCodes.Status404NotFound);
        objectResult.ContentTypes.ShouldContain("application/problem+json");
        problem.Status.ShouldBe(StatusCodes.Status404NotFound);
        problem.Type.ShouldBe("https://errors.example.com/widget-not-found");
        problem.Title.ShouldBe("Not Found");
        problem.Detail.ShouldBe("The widget was not found.");
        problem.Instance.ShouldBe("/widgets/42");
    }

    [Fact]
    public void ToActionResult_ValidationFailure_GroupsMessagesAndCodesByTarget()
    {
        var controller = CreateController(options => options.BaseTypeUri = "https://errors.example.com");
        var result = Result.Failure(
            new ResultError("name-required", "A name is required.", ResultErrorKind.Validation, "name"),
            new ResultError("name-too-long", "The name is too long.", ResultErrorKind.Validation, "name"),
            new ResultError("request-invalid", "The request is invalid.", ResultErrorKind.Validation));

        var actionResult = result.ToActionResult(controller, () => controller.NoContent());

        var objectResult = GetObjectResult(actionResult);
        var problem = objectResult.Value.ShouldBeOfType<ValidationProblemDetails>();
        objectResult.StatusCode.ShouldBe(StatusCodes.Status400BadRequest);
        objectResult.ContentTypes.ShouldContain("application/problem+json");
        problem.Type.ShouldBe("https://errors.example.com/validation-failed");
        problem.Title.ShouldBe("One or more validation errors occurred.");
        problem.Detail.ShouldBe("A name is required.");
        problem.Instance.ShouldBe("/widgets/42");
        problem.Errors["name"].ShouldBe(["A name is required.", "The name is too long."]);
        problem.Errors[string.Empty].ShouldBe(["The request is invalid."]);

        var errorCodes = problem.Extensions["errorCodes"]
            .ShouldBeOfType<Dictionary<string, string[]>>();
        errorCodes["name"].ShouldBe(["name-required", "name-too-long"]);
        errorCodes[string.Empty].ShouldBe(["request-invalid"]);
    }

    [Fact]
    public void ToActionResult_CustomStatusMapper_IsUsed()
    {
        var controller = CreateController(options => options.StatusCodeMapper = _ => 418);
        var result = Result.Failure(new ResultError(
            "brew-failed",
            "The teapot declined.",
            ResultErrorKind.Failure));

        var actionResult = result.ToActionResult(controller, () => controller.NoContent());

        GetObjectResult(actionResult).StatusCode.ShouldBe(418);
    }

    [Fact]
    public void ToActionResult_WithoutRegistration_ThrowsClearException()
    {
        using var provider = new ServiceCollection().BuildServiceProvider();
        var controller = CreateController(provider);

        var exception = Should.Throw<InvalidOperationException>(() =>
            Result.Success().ToActionResult(controller, () => controller.NoContent()));

        exception.Message.ShouldContain(nameof(ResultProblemDetailsServiceCollectionExtensions.AddResultProblemDetails));
    }

    private static TestController CreateController(Action<ResultProblemDetailsOptions>? configure = null)
    {
        var services = new ServiceCollection();
        services.AddResultProblemDetails(configure);
        return CreateController(services.BuildServiceProvider());
    }

    private static TestController CreateController(IServiceProvider provider)
    {
        var context = new DefaultHttpContext
        {
            RequestServices = provider,
        };
        context.Request.Path = "/widgets/42";

        return new TestController
        {
            ControllerContext = new ControllerContext { HttpContext = context },
        };
    }

    private static ObjectResult GetObjectResult(IActionResult result) =>
        result.ShouldBeOfType<ObjectResult>();

    private sealed class TestController : ControllerBase
    {
    }
}
