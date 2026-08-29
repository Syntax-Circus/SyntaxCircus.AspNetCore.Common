# ApiResult Status Passthrough (SyntaxCircus.AspNetCore.Common) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add `ToActionResult` overloads for `ApiResult`/`ApiResult<T>` (new types added to `SyntaxCircus.Common` by the companion plan in that repo) so a failed passthrough result renders as a `ProblemDetails` at its caller-supplied exact HTTP status code, instead of going through the fixed `ResultErrorKind` → status mapping.

**Architecture:** `ResultProblemDetailsMapper` gains an `internal MapWithStatus(ControllerBase, ResultError, int)` method — the same `ProblemDetails` construction the existing `MapProblem` already does, but taking the status code as a parameter instead of deriving it from `StatusCodeMapper`. `MapProblem` becomes a one-line wrapper over it. `ResultActionResultExtensions` gains `ToActionResult`/`ToActionResult<T>` overloads for `ApiResult`/`ApiResult<T>`, resolved by ordinary C# overload resolution, which call `MapWithStatus` with `result.StatusCode` on failure. `ResultProblemDetailsOptions.DefaultStatusCodeMapper` gains a `Passthrough => 500` branch purely to stay exhaustive.

**Tech Stack:** .NET 10, ASP.NET Core, xunit.v3, Shouldly, Central Package Management (`Directory.Packages.props`).

**Spec:** `../SyntaxCircus.Common/docs/superpowers/specs/2026-08-28-apiresult-status-passthrough-design.md` (lives in the `SyntaxCircus.Common` repo, since that is where the type shape is defined; this plan implements the HTTP-mapping half of that same spec).

## Global Constraints

- Depends on `SyntaxCircus.Common`'s `ApiResult`/`ApiResult<T>` (companion plan, separate repo/branch, must be packed via that plan's Task 4 before this plan's Task 2 onward can compile).
- No change to the existing `Result`/`Result<T>` `ToActionResult` overloads or their tests — this is purely additive.
- Failure responses render as the same `ProblemDetails` shape the rest of the mapper produces (`Type`/`Title`/`Detail`/`Instance`, `application/problem+json` content type) — no bespoke payload support (per spec's "Non-goals").
- This plan does not touch sinforgiver or `_template` (per spec's "Rollout scope").
- **Temporary, pre-publish state:** until `SyntaxCircus.Common`'s new version is actually published to nuget.org (which only happens via that repo's CI on a merge to its `main`), this repo's `Directory.Packages.props` pin and restore sources will point at a local, unpublished build. Task 5 exists specifically to reverse that once the real version is available — do not merge this branch to `main` before Task 5 is done.

---

### Task 1: Point local restore at the `SyntaxCircus.Common` local feed

**Files:**
- Modify: `Directory.Packages.props`

**Interfaces:**
- Consumes: the `.nupkg` and exact version string produced by the `SyntaxCircus.Common` plan's Task 4, in `D:\dev\SyntaxCircus\.worktrees\result-local-feed`.
- Produces: a working local build/test loop for Tasks 2–4 below, which reference `ApiResult`/`ApiResult<T>`.

No NuGet.Config file is created or committed for this — the local feed is passed explicitly via `--source` on each `dotnet restore`, so nothing machine-specific lands in git history.

- [ ] **Step 1: Confirm baseline is green before touching anything**

Run: `dotnet restore SyntaxCircus.AspNetCore.Common.slnx`
Run: `dotnet build SyntaxCircus.AspNetCore.Common.slnx --no-restore --configuration Release`
Run: `dotnet test --solution SyntaxCircus.AspNetCore.Common.slnx --no-build --configuration Release`
Expected: All PASS (matching current `main`, since nothing has changed yet).

- [ ] **Step 2: Bump the `SyntaxCircus.Common` pin to the local build's version**

In `Directory.Packages.props`, change:

```xml
    <PackageVersion Include="SyntaxCircus.Common" Version="[0.1.2]" />
```

to (substituting the exact version string recorded by the `SyntaxCircus.Common` plan's Task 4 — e.g. if that step recorded `0.2.0-api-result-status-passthrough.1`):

```xml
    <PackageVersion Include="SyntaxCircus.Common" Version="[0.2.0-api-result-status-passthrough.1]" />
```

- [ ] **Step 3: Restore against the local feed**

Run:

```bash
dotnet restore SyntaxCircus.AspNetCore.Common.slnx --source "D:\dev\SyntaxCircus\.worktrees\result-local-feed" --source "https://api.nuget.org/v3/index.json"
```

Expected: Restore succeeds and resolves `SyntaxCircus.Common` to the version from Step 2 (no error about an unresolvable package version).

- [ ] **Step 4: Build and run the existing suite against the new dependency version**

Run: `dotnet build SyntaxCircus.AspNetCore.Common.slnx --no-restore --configuration Release`
Run: `dotnet test --solution SyntaxCircus.AspNetCore.Common.slnx --no-build --configuration Release`
Expected: All PASS — this is a version bump only so far (no `ApiResult` usage added yet), so behavior is unchanged.

- [ ] **Step 5: Commit**

```bash
git add Directory.Packages.props
git commit -m "chore: point SyntaxCircus.Common at local build for ApiResult development"
```

---

### Task 2: `ResultProblemDetailsMapper.MapWithStatus`

**Files:**
- Modify: `src/SyntaxCircus.AspNetCore.Common/ResultProblemDetailsMapper.cs`

**Interfaces:**
- Consumes: nothing new (same `ResultError`, `ResultProblemDetailsOptions`, `ReasonPhrases` already used by `MapProblem`).
- Produces: `internal ObjectResult MapWithStatus(ControllerBase controller, ResultError error, int statusCode)`, callable from `ResultActionResultExtensions.cs` (same assembly). Task 3 calls this directly.

There's no new externally-observable behavior in this task by itself — `MapProblem`'s existing tests (`ResultActionResultExtensionsTests.ToActionResult_NonValidationFailure_CreatesProblemDetails`, etc.) are the regression check, since `MapProblem` becomes a thin wrapper with identical output.

- [ ] **Step 1: Confirm baseline is green**

Run: `dotnet test tests/SyntaxCircus.AspNetCore.Common.Tests --filter FullyQualifiedName~ResultActionResultExtensionsTests`
Expected: PASS.

- [ ] **Step 2: Extract `MapWithStatus` from `MapProblem`**

In `src/SyntaxCircus.AspNetCore.Common/ResultProblemDetailsMapper.cs`, change:

```csharp
    private ObjectResult MapProblem(ControllerBase controller, ResultError error, int statusCode)
    {
        var problem = new ProblemDetails
        {
            Status = statusCode,
            Type = _options.BuildTypeUri(error.Code),
            Title = ReasonPhrases.GetReasonPhrase(statusCode),
            Detail = error.Message,
            Instance = controller.HttpContext.Request.Path.Value,
        };

        return CreateObjectResult(problem, statusCode);
    }
```

to:

```csharp
    private ObjectResult MapProblem(ControllerBase controller, ResultError error, int statusCode) =>
        MapWithStatus(controller, error, statusCode);

    internal ObjectResult MapWithStatus(ControllerBase controller, ResultError error, int statusCode)
    {
        var problem = new ProblemDetails
        {
            Status = statusCode,
            Type = _options.BuildTypeUri(error.Code),
            Title = ReasonPhrases.GetReasonPhrase(statusCode),
            Detail = error.Message,
            Instance = controller.HttpContext.Request.Path.Value,
        };

        return CreateObjectResult(problem, statusCode);
    }
```

- [ ] **Step 3: Run tests to verify no regression**

Run: `dotnet test tests/SyntaxCircus.AspNetCore.Common.Tests --filter FullyQualifiedName~ResultActionResultExtensionsTests`
Expected: PASS (identical to Step 1).

- [ ] **Step 4: Commit**

```bash
git add src/SyntaxCircus.AspNetCore.Common/ResultProblemDetailsMapper.cs
git commit -m "refactor: extract ResultProblemDetailsMapper.MapWithStatus from MapProblem"
```

---

### Task 3: `ToActionResult` overloads for `ApiResult`/`ApiResult<T>`

**Files:**
- Modify: `src/SyntaxCircus.AspNetCore.Common/ResultActionResultExtensions.cs`
- Modify: `tests/SyntaxCircus.AspNetCore.Common.Tests/GlobalUsings.cs` (add `System.Net` for `HttpStatusCode`)
- Create: `tests/SyntaxCircus.AspNetCore.Common.Tests/ApiResultActionResultExtensionsTests.cs`

**Interfaces:**
- Consumes: `ApiResult`/`ApiResult<T>` from `SyntaxCircus.Common` (`IsSuccess`, `Value`, `Errors`, `StatusCode`); `ResultProblemDetailsMapper.MapWithStatus` from Task 2; the existing private `GetMapper(ControllerBase)` helper already in this file.
- Produces: `ToActionResult(this ApiResult, ControllerBase, Func<IActionResult>)` and `ToActionResult<T>(this ApiResult<T>, ControllerBase, Func<T, IActionResult>)`, both public.

- [ ] **Step 1: Write the failing tests**

Add `System.Net` to `tests/SyntaxCircus.AspNetCore.Common.Tests/GlobalUsings.cs`:

```csharp
global using System.Net;
global using System.Security.Claims;
global using Microsoft.AspNetCore.Builder;
global using Microsoft.AspNetCore.Hosting;
global using Microsoft.AspNetCore.Http;
global using Microsoft.AspNetCore.HttpOverrides;
global using Microsoft.AspNetCore.Mvc;
global using Microsoft.AspNetCore.Routing;
global using Microsoft.AspNetCore.TestHost;
global using Microsoft.Extensions.Configuration;
global using Microsoft.Extensions.DependencyInjection;
global using Microsoft.Extensions.Hosting;
global using Microsoft.Extensions.Logging;
global using Microsoft.Extensions.Options;
global using NSubstitute;
global using Shouldly;
global using SyntaxCircus.AspNetCore.Common;
global using SyntaxCircus.AspNetCore.Common.Tests.Infrastructure;
global using SyntaxCircus.Common;
global using Xunit;
```

Create `tests/SyntaxCircus.AspNetCore.Common.Tests/ApiResultActionResultExtensionsTests.cs`:

```csharp
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
        problem.Status.ShouldBe(expectedStatus);
        problem.Detail.ShouldBe("The upstream service returned an error.");
        problem.Instance.ShouldBe("/widgets/42");
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
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/SyntaxCircus.AspNetCore.Common.Tests --filter FullyQualifiedName~ApiResultActionResultExtensionsTests`
Expected: FAIL — build error, no `ToActionResult` overload accepts `ApiResult`/`ApiResult<T>` yet.

- [ ] **Step 3: Add the overloads**

In `src/SyntaxCircus.AspNetCore.Common/ResultActionResultExtensions.cs`, add these two methods to the `ResultActionResultExtensions` class, after the existing `ToActionResult<T>(this Result<T> ...)` overload and before `GetMapper`:

```csharp
    public static IActionResult ToActionResult(
        this ApiResult result,
        ControllerBase controller,
        Func<IActionResult> onSuccess)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(controller);
        ArgumentNullException.ThrowIfNull(onSuccess);

        if (result.IsSuccess)
        {
            return onSuccess();
        }

        var mapper = GetMapper(controller);
        return mapper.MapWithStatus(controller, result.Errors[0], (int)result.StatusCode!.Value);
    }

    public static IActionResult ToActionResult<T>(
        this ApiResult<T> result,
        ControllerBase controller,
        Func<T, IActionResult> onSuccess)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(controller);
        ArgumentNullException.ThrowIfNull(onSuccess);

        if (result.IsSuccess)
        {
            return onSuccess(result.Value);
        }

        var mapper = GetMapper(controller);
        return mapper.MapWithStatus(controller, result.Errors[0], (int)result.StatusCode!.Value);
    }
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/SyntaxCircus.AspNetCore.Common.Tests --filter FullyQualifiedName~ApiResultActionResultExtensionsTests`
Expected: PASS (all 15 test cases, counting `[Theory]` rows individually).

- [ ] **Step 5: Run the full test project to check for regressions**

Run: `dotnet test tests/SyntaxCircus.AspNetCore.Common.Tests`
Expected: PASS (all tests, including the existing `ResultActionResultExtensionsTests`).

- [ ] **Step 6: Commit**

```bash
git add src/SyntaxCircus.AspNetCore.Common/ResultActionResultExtensions.cs tests/SyntaxCircus.AspNetCore.Common.Tests/GlobalUsings.cs tests/SyntaxCircus.AspNetCore.Common.Tests/ApiResultActionResultExtensionsTests.cs
git commit -m "feat: add ToActionResult overloads for ApiResult/ApiResult<T>"
```

---

### Task 4: `DefaultStatusCodeMapper` exhaustiveness for `Passthrough`

**Files:**
- Modify: `src/SyntaxCircus.AspNetCore.Common/ResultProblemDetailsOptions.cs`
- Modify: `tests/SyntaxCircus.AspNetCore.Common.Tests/ResultActionResultExtensionsTests.cs`

**Interfaces:**
- Consumes: `ResultErrorKind.Passthrough` (from the `SyntaxCircus.Common` plan).
- Produces: nothing new consumed elsewhere — this only prevents `DefaultStatusCodeMapper` from throwing if a raw `ResultError` with `ResultErrorKind.Passthrough` is ever pushed through the *ordinary* `Result`/`Result<T>` → `ToActionResult` path (bypassing `ApiResult` entirely).

- [ ] **Step 1: Write the failing test**

In `tests/SyntaxCircus.AspNetCore.Common.Tests/ResultActionResultExtensionsTests.cs`, add a row to the existing `ToActionResult_Failure_UsesDefaultStatusMapping` theory:

```csharp
    [Theory]
    [InlineData(ResultErrorKind.Validation, 400)]
    [InlineData(ResultErrorKind.Unauthenticated, 401)]
    [InlineData(ResultErrorKind.Forbidden, 403)]
    [InlineData(ResultErrorKind.NotFound, 404)]
    [InlineData(ResultErrorKind.Conflict, 409)]
    [InlineData(ResultErrorKind.Failure, 500)]
    [InlineData(ResultErrorKind.Passthrough, 500)]
    public void ToActionResult_Failure_UsesDefaultStatusMapping(ResultErrorKind kind, int expectedStatus)
```

(only the new `[InlineData(ResultErrorKind.Passthrough, 500)]` line is added; the method body is unchanged.)

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/SyntaxCircus.AspNetCore.Common.Tests --filter FullyQualifiedName~ToActionResult_Failure_UsesDefaultStatusMapping`
Expected: FAIL on the new `Passthrough` case — `DefaultStatusCodeMapper` throws `ArgumentOutOfRangeException` because its switch isn't exhaustive yet.

- [ ] **Step 3: Add the `Passthrough` branch**

In `src/SyntaxCircus.AspNetCore.Common/ResultProblemDetailsOptions.cs`, change:

```csharp
    private static int DefaultStatusCodeMapper(ResultErrorKind kind) => kind switch
    {
        ResultErrorKind.Validation => StatusCodes.Status400BadRequest,
        ResultErrorKind.Unauthenticated => StatusCodes.Status401Unauthorized,
        ResultErrorKind.Forbidden => StatusCodes.Status403Forbidden,
        ResultErrorKind.NotFound => StatusCodes.Status404NotFound,
        ResultErrorKind.Conflict => StatusCodes.Status409Conflict,
        ResultErrorKind.Failure => StatusCodes.Status500InternalServerError,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "The result error kind is not defined."),
    };
```

to:

```csharp
    private static int DefaultStatusCodeMapper(ResultErrorKind kind) => kind switch
    {
        ResultErrorKind.Validation => StatusCodes.Status400BadRequest,
        ResultErrorKind.Unauthenticated => StatusCodes.Status401Unauthorized,
        ResultErrorKind.Forbidden => StatusCodes.Status403Forbidden,
        ResultErrorKind.NotFound => StatusCodes.Status404NotFound,
        ResultErrorKind.Conflict => StatusCodes.Status409Conflict,
        ResultErrorKind.Failure => StatusCodes.Status500InternalServerError,
        ResultErrorKind.Passthrough => StatusCodes.Status500InternalServerError,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "The result error kind is not defined."),
    };
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test tests/SyntaxCircus.AspNetCore.Common.Tests --filter FullyQualifiedName~ToActionResult_Failure_UsesDefaultStatusMapping`
Expected: PASS (all 7 rows).

- [ ] **Step 5: Run the full test project to check for regressions**

Run: `dotnet test tests/SyntaxCircus.AspNetCore.Common.Tests`
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add src/SyntaxCircus.AspNetCore.Common/ResultProblemDetailsOptions.cs tests/SyntaxCircus.AspNetCore.Common.Tests/ResultActionResultExtensionsTests.cs
git commit -m "fix: make DefaultStatusCodeMapper exhaustive for ResultErrorKind.Passthrough"
```

---

### Task 5: Finalize the `SyntaxCircus.Common` version pin once published

**Files:**
- Modify: `Directory.Packages.props`

**Interfaces:**
- Consumes: the real, published `SyntaxCircus.Common` NuGet package version (only exists after that repo's PR merges to its `main` and its CI publish job runs).
- Produces: a mergeable state for this branch — `Directory.Packages.props` pinned to a real published version, no dependency on the local feed.

This task is gated on an external event (the companion repo's release), not on anything in this repo. Do not open this branch's PR, and do not merge it, until this task is done.

- [ ] **Step 1: Check whether the new version is published**

Run: `dotnet package search SyntaxCircus.Common --exact-match --source https://api.nuget.org/v3/index.json`

Expected once ready: the listed latest version is the one produced by the `SyntaxCircus.Common` plan's release (a real version like `0.2.0`, not the local prerelease string used during development — GitVersion assigns the real released version only once that repo's changes land on its `main`). If the listed latest version is still the old one (`0.1.2`), the companion repo's PR hasn't merged/published yet — wait and re-check later.

- [ ] **Step 2: Update the pin to the real published version**

In `Directory.Packages.props`, change the temporary local pin from Task 1 (e.g. `Version="[0.2.0-api-result-status-passthrough.1]"`) to the real published version found in Step 1 (e.g. `Version="[0.2.0]"`).

- [ ] **Step 3: Restore against nuget.org only (no local feed)**

Run: `dotnet restore SyntaxCircus.AspNetCore.Common.slnx`
Expected: Restore succeeds using only the repo's normal configured sources — no `--source` flags needed anymore, since the real package is now on nuget.org.

- [ ] **Step 4: Build and run the full suite**

Run: `dotnet build SyntaxCircus.AspNetCore.Common.slnx --no-restore --configuration Release`
Run: `dotnet test --solution SyntaxCircus.AspNetCore.Common.slnx --no-build --configuration Release`
Expected: All PASS, identical results to Task 4's Step 5.

- [ ] **Step 5: Commit**

```bash
git add Directory.Packages.props
git commit -m "chore: pin SyntaxCircus.Common to the published version"
```

## Self-Review Notes

- **Spec coverage:** `MapWithStatus`/`MapProblem` refactor (Task 2), `ToActionResult` overloads (Task 3), `DefaultStatusCodeMapper` exhaustiveness (Task 4) cover the entirety of the spec's "HTTP mapping" and the AspNetCore.Common half of "Error kind." The type-shape and error-kind-definition halves of the spec belong to the companion `SyntaxCircus.Common` plan.
- **Placeholder scan:** No TBD/TODO. Task 5 is gated on an external event by nature (a separate repo's release), not a placeholder — it gives the exact command to check readiness and the exact edit to make once ready.
- **Type consistency:** `MapWithStatus(ControllerBase, ResultError, int)`'s signature (Task 2) matches its two call sites added in Task 3 exactly (`mapper.MapWithStatus(controller, result.Errors[0], (int)result.StatusCode!.Value)`).
- **Cross-repo dependency:** Task 1 cannot proceed until the `SyntaxCircus.Common` plan's Task 4 has produced a packed build and version string; Task 5 cannot proceed until that repo's change has actually been merged and published. Both are called out explicitly in Global Constraints and in the tasks themselves.
