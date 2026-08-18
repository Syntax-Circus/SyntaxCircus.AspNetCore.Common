# SyntaxCircus.AspNetCore.Common

[![Build](https://github.com/Syntax-Circus/SyntaxCircus.AspNetCore.Common/actions/workflows/build.yml/badge.svg)](https://github.com/Syntax-Circus/SyntaxCircus.AspNetCore.Common/actions/workflows/build.yml)
[![NuGet](https://img.shields.io/nuget/v/SyntaxCircus.AspNetCore.Common.svg)](https://www.nuget.org/packages/SyntaxCircus.AspNetCore.Common)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE.txt)

The small pieces of ASP.NET Core host boilerplate that show up in nearly every project, in one place: correlation-ID middleware, security headers, a composable exception-handler/HSTS bootstrap, RFC 7807 ProblemDetails exception handling, trusted-proxy validation, standard health check endpoints, and rate-limiting policy helpers.

> **No support guaranteed.** Published as-is and maintained on a best-effort basis. Issues and PRs are welcome, but there's no SLA — fork it or vendor what you need if that's not enough.

## Correlation ID

```csharp
builder.Services.AddCorrelationId(); // optionally: options => options.HeaderName = "X-My-Correlation-Id"

var app = builder.Build();
app.UseCorrelationId();
```

Reads (or generates) a correlation ID per request, echoes it on the response header, tags the current `Activity`, and pushes it — with the current trace/span ID — into the logger scope for the rest of the request, so downstream log lines carry it automatically. `SyntaxCircus.AspNetCore.Common.CorrelationContextAccessor.CurrentCorrelationId` gives ambient (AsyncLocal) access to it outside the middleware pipeline.

## Security headers

```csharp
builder.Services.AddSecurityHeaders(builder.Configuration); // binds the "SecurityHeaders" section

var app = builder.Build();
app.UseSecurityHeaders();
```

Sets `Referrer-Policy`, `X-Frame-Options`, `X-Content-Type-Options`, `Permissions-Policy`, `Content-Security-Policy`, and `Strict-Transport-Security` from `SecurityHeadersOptions`, with sensible defaults you can override per-key in configuration. `X-Robots-Tag` is also supported (`RobotsTag`) but omitted entirely unless set.

Some routes need different values than the rest of the app — e.g. a public, unauthenticated page that must never leak a referrer or get indexed. Configure `PathOverrides` for that instead of writing a second middleware:

```json
{
  "SecurityHeaders": {
    "PathOverrides": [
      { "PathPrefix": "/public-profile", "ReferrerPolicy": "no-referrer", "RobotsTag": "noindex, nofollow" }
    ]
  }
}
```

The first entry whose `PathPrefix` matches the request path (via `PathString.StartsWithSegments`) wins; everything else keeps the top-level defaults.

## Exception handling / HSTS bootstrap

```csharp
var app = builder.Build();
app.UseStandardExceptionHandling(); // "/error", HSTS — skipped entirely in Development
```

Bundles `UseExceptionHandler(errorPath)` + `UseHsts()`, both skipped in Development, with an optional status-code re-execute page (`useStatusCodePages: true`). It's a plain extension method, not something wired in automatically — a pure API host behind a reverse proxy that already terminates TLS and handles error pages doesn't need to call it.

## ProblemDetails exception handling

```csharp
builder.Services.AddProblemDetailsExceptionHandling(options =>
{
    options.BaseTypeUri = "https://errors.example.com";
    options.ExceptionMapper = ex => ex switch
    {
        NotFoundException => new ProblemMapping(StatusCodes.Status404NotFound, "not-found"),
        _ => new ProblemMapping(StatusCodes.Status500InternalServerError, "internal-error"),
    };
});

var app = builder.Build();
app.UseProblemDetailsExceptionHandling();
```

Catches unhandled exceptions and writes an RFC 7807 `ProblemDetails` response, with `Type` built from `BaseTypeUri` + your error code. Which exception types mean what status/code is deliberately a delegate you supply — that mapping is product-specific and the package doesn't try to guess it for you. A reasonable default mapper is provided if you don't set one, and it never puts a raw `ex.Message` into the response body: every case — including the unmapped/500 fallback — gets an explicit, generic `Detail` string. This matters because `ex.Message` on an exception nobody anticipated (a database error, a file path, connection details) can carry internals that shouldn't reach an API client.

The same rule applies to custom mappers: if your `ExceptionMapper` leaves a `ProblemMapping`'s `Detail` unset (`null`), the middleware leaves `Detail` `null` in the response too — it does not silently substitute `ex.Message`. If you want the old fall-back-to-`ex.Message` behavior for cases your mapper doesn't set `Detail` for, opt in explicitly:

```csharp
builder.Services.AddProblemDetailsExceptionHandling(options =>
{
    options.IncludeExceptionMessageInDetail = true; // restores ex.Message fallback when a mapping's Detail is null
});
```

`IncludeExceptionMessageInDetail` defaults to `false`. Only set it to `true` if you've verified your exception messages are safe to expose to API clients (e.g. gated to non-production environments).

## Trusted-proxy validation

```csharp
builder.Services.AddTrustedProxyForwardedHeaders(builder.Configuration); // binds the "TrustedProxy" section

var app = builder.Build();
app.UseForwardedHeaders();
```

Wires `ForwardedHeadersOptions` (`X-Forwarded-For`/`-Proto`/`-Host`) from `TrustedProxies`/`TrustedNetworks`, and automatically fails fast at startup if you're running behind a reverse proxy without telling ASP.NET Core which upstream hosts to actually trust — with neither configured, forwarded headers would otherwise be trusted from anyone. This validation runs on its own (via a registered `IStartupFilter`); you don't need to call anything else for it to take effect.

If you want to trigger the same check outside the normal startup path (e.g. in a test), `ValidateTrustedProxyConfiguration` is available directly:

```csharp
builder.Environment.ValidateTrustedProxyConfiguration(trustedProxyOptions); // throws outside Development if misconfigured
```

## Health checks

```csharp
var app = builder.Build();
app.MapStandardHealthChecks(); // /health/live (no checks run) and /health/ready (checks tagged "ready")
```

Maps standard liveness/readiness endpoints rendered via `HealthCheckResponseWriter` (status, total duration, and per-check name/status/duration/description as JSON). Register your own `IHealthCheck` implementations with `AddHealthChecks().AddCheck<T>(tags: ["ready"])` as usual — this just standardizes the endpoints and response shape.

Pass `metadataFactory` to include extra data (e.g. an app version) under a `metadata` key — invoked per-request, omitted entirely when not supplied:

```csharp
app.MapStandardHealthChecks(metadataFactory: _ => new Dictionary<string, object?> { ["version"] = appVersion });
```

## Rate limiting

```csharp
builder.Services.AddRateLimiter(options =>
{
    options.AddPerIpFixedWindow("public", permitLimit: 60, window: TimeSpan.FromMinutes(1));
    options.AddPerSubjectFixedWindow("authenticated", permitLimit: 600, window: TimeSpan.FromMinutes(1));
    options.UseProblemDetailsRejection();
});

var app = builder.Build();
app.UseRateLimiter();
```

### API surface

| Method | Purpose |
| --- | --- |
| `AddPerIpFixedWindow(policyName, permitLimit, window)` | Fixed-window policy partitioned by remote IP (`"unknown"` fallback). |
| `AddPerIpFixedWindow(policyName, permitLimit, window, configure)` | Same as above, with per-policy `FixedWindowRateLimiterOptions` overrides. |
| `AddPerSubjectFixedWindow(policyName, permitLimit, window)` | Fixed-window policy partitioned by authenticated subject (`sub`, fallback `NameIdentifier`, then remote IP). |
| `AddPerSubjectFixedWindow(policyName, permitLimit, window, configure)` | Same as above, with per-policy `FixedWindowRateLimiterOptions` overrides. |
| `AddPartitionedFixedWindow(policyName, partitionKeySelector, permitLimit, window, configure = null)` | Additive advanced API for custom partition key composition (route, claim combinations, tenant headers, etc.). |
| `UseProblemDetailsRejection(errorCode = "rate-limited")` | Writes ProblemDetails 429 responses and includes `Retry-After` when available. |

### Advanced partition-key composition (additive, non-breaking)

```csharp
builder.Services.AddRateLimiter(options =>
{
    options.AddPartitionedFixedWindow(
        "tenant-route",
        ctx => $"{ctx.User.FindFirst("tenant_id")?.Value ?? "anon"}:{ctx.Request.Path}",
        permitLimit: 120,
        window: TimeSpan.FromMinutes(1),
        configure: fixedWindow =>
        {
            fixedWindow.QueueLimit = 5;
            fixedWindow.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        });

    options.UseProblemDetailsRejection();
});
```

This keeps the existing convenience helpers intact while adding custom partition selection and optional per-policy overrides when app-level policy composition needs to be richer.

## Contributing

Issues and pull requests are welcome:
- Keep changes focused, with a clear description of the behavior change.
- Match the existing code style (see `.editorconfig`).
- Call out any breaking changes to the public API in your PR description.

## License

MIT — see [LICENSE.txt](LICENSE.txt).
