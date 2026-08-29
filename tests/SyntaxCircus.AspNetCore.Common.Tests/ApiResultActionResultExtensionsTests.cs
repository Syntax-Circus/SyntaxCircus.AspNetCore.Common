namespace SyntaxCircus.AspNetCore.Common.Tests;

public class ApiResultActionResultExtensionsTests
{
    [Theory]
    [InlineData(HttpStatusCode.BadRequest, 400)]
    [InlineData(HttpStatusCode.NotFound, 404)]
    [InlineData(HttpStatusCode.TooManyRequests, 429)]
    [InlineData(HttpStatusCode.BadGateway, 502)]
    [InlineData(HttpStatusCode.ServiceUnavailable, 503)]
    [InlineData(HttpStatusCode.RequestEntityTooLarge, 413)]
    public void ToActionResult_Failure_UsesExactPassthroughStatus(HttpStatusCode statusCode, int expectedStatus)
    {
        var controller = CreateController();
        var result = ApiResult.Failure(statusCode, "upstream-error", "The upstream service returned an error.");

        var actionResult = result.ToActionResult(controller, () => controller.NoContent());

        var objectResult = GetObjectResult(actionResult);
        var problem = objectResult.Value.ShouldBeOfType<ProblemDetails>();
        objectResult.StatusCode.ShouldBe(expectedStatus);
        problem.Title.ShouldNotBeNullOrEmpty();
        problem.Status.ShouldBe(expectedStatus);
        problem.Detail.ShouldBe("The upstream service returned an error.");
        problem.Instance.ShouldBe("/widgets/42");
    }

    [Fact]
    public void ToActionResult_Failure_FallsBackToGenericTitleForNonStandardStatus()
    {
        var controller = CreateController();
        var result = ApiResult.Failure((HttpStatusCode)520, "upstream-error", "The upstream service returned an error.");

        var actionResult = result.ToActionResult(controller, () => controller.NoContent());

        var objectResult = GetObjectResult(actionResult);
        var problem = objectResult.Value.ShouldBeOfType<ProblemDetails>();
        objectResult.StatusCode.ShouldBe(520);
        problem.Title.ShouldBe("Error");
    }

    [Fact]
    public void ToActionResult_NonGenericSuccess_InvokesCallback()
    {
        var controller = CreateController();
        var callbackInvoked = false;

        var actionResult = ApiResult.Success().ToActionResult(controller, () =>
        {
            callbackInvoked = true;
            return controller.NoContent();
        });

        callbackInvoked.ShouldBeTrue();
        actionResult.ShouldBeOfType<NoContentResult>();
    }

    [Fact]
    public void ToActionResult_NonGenericFailure_DoesNotInvokeSuccessCallback()
    {
        var controller = CreateController();
        var callbackInvoked = false;
        var result = ApiResult.Failure(HttpStatusCode.BadGateway, "upstream-error", "The upstream service returned an error.");

        _ = result.ToActionResult(controller, () =>
        {
            callbackInvoked = true;
            return controller.NoContent();
        });

        callbackInvoked.ShouldBeFalse();
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest, 400)]
    [InlineData(HttpStatusCode.NotFound, 404)]
    [InlineData(HttpStatusCode.TooManyRequests, 429)]
    [InlineData(HttpStatusCode.BadGateway, 502)]
    [InlineData(HttpStatusCode.ServiceUnavailable, 503)]
    public void ToActionResult_GenericFailure_UsesExactPassthroughStatus(HttpStatusCode statusCode, int expectedStatus)
    {
        var controller = CreateController();
        var result = ApiResult<string>.Failure(statusCode, "upstream-error", "The upstream service returned an error.");

        var actionResult = result.ToActionResult(controller, value => controller.Ok(value));

        var objectResult = GetObjectResult(actionResult);
        objectResult.StatusCode.ShouldBe(expectedStatus);
    }

    [Fact]
    public void ToActionResult_GenericSuccess_InvokesCallbackWithValue()
    {
        var controller = CreateController();
        var result = ApiResult<string>.Success("accepted");
        string? callbackValue = null;

        var actionResult = result.ToActionResult(controller, value =>
        {
            callbackValue = value;
            return controller.Ok(value);
        });

        callbackValue.ShouldBe("accepted");
        actionResult.ShouldBeOfType<OkObjectResult>();
    }

    [Fact]
    public void ToActionResult_GenericFailure_DoesNotInvokeSuccessCallback()
    {
        var controller = CreateController();
        var callbackInvoked = false;
        var result = ApiResult<string>.Failure(HttpStatusCode.BadGateway, "upstream-error", "The upstream service returned an error.");

        _ = result.ToActionResult(controller, value =>
        {
            callbackInvoked = true;
            return controller.Ok(value);
        });

        callbackInvoked.ShouldBeFalse();
    }

    [Fact]
    public void ToActionResult_SuccessWithoutRegistration_ThrowsClearException()
    {
        using var provider = new ServiceCollection().BuildServiceProvider();
        var context = new DefaultHttpContext { RequestServices = provider };
        context.Request.Path = "/widgets/42";
        var controller = new TestController { ControllerContext = new ControllerContext { HttpContext = context } };

        var exception = Should.Throw<InvalidOperationException>(() =>
            ApiResult.Success().ToActionResult(controller, () => controller.NoContent()));

        exception.Message.ShouldContain(nameof(ResultProblemDetailsServiceCollectionExtensions.AddResultProblemDetails));
    }

    private static TestController CreateController()
    {
        var services = new ServiceCollection();
        services.AddResultProblemDetails();
        var provider = services.BuildServiceProvider();
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
