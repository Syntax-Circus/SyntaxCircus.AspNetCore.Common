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

Sets `Referrer-Policy`, `X-Frame-Options`, `X-Content-Type-Options`, `Permissions-Policy`, `Content-Security-Policy`, and `Strict-Transport-Security` from `SecurityHeadersOptions`, with sensible defaults you can override per-key in configuration.

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

Catches unhandled exceptions and writes an RFC 7807 `ProblemDetails` response, with `Type` built from `BaseTypeUri` + your error code. Which exception types mean what status/code is deliberately a delegate you supply — that mapping is product-specific and the package doesn't try to guess it for you. A reasonable default mapper is provided if you don't set one.

## Trusted-proxy validation

```csharp
builder.Services.AddTrustedProxyForwardedHeaders(builder.Configuration); // binds the "TrustedProxy" section

var trustedProxyOptions = new TrustedProxyOptions();
builder.Configuration.GetSection(TrustedProxyOptions.SectionName).Bind(trustedProxyOptions);
builder.Environment.ValidateTrustedProxyConfiguration(trustedProxyOptions); // throws outside Development if misconfigured

var app = builder.Build();
app.UseForwardedHeaders();
```

Fails fast at startup if you're running behind a reverse proxy without telling ASP.NET Core which upstream hosts to actually trust — without `TrustedProxies`/`TrustedNetworks` configured, forwarded headers (`X-Forwarded-For`/`-Proto`) would otherwise be trusted from anyone.

## Health checks

```csharp
var app = builder.Build();
app.MapStandardHealthChecks(); // /health/live (no checks run) and /health/ready (checks tagged "ready")
```

Maps standard liveness/readiness endpoints rendered via `HealthCheckResponseWriter` (status, total duration, and per-check name/status/duration/description as JSON). Register your own `IHealthCheck` implementations with `AddHealthChecks().AddCheck<T>(tags: ["ready"])` as usual — this just standardizes the endpoints and response shape.

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

Policy-factory helpers for the two most common partitioning strategies (per-IP, per-authenticated-subject with IP fallback when anonymous), plus a rejection handler that writes a ProblemDetails 429 (with `Retry-After` when the limiter knows it), reusing the same `ProblemDetailsMappingOptions.BuildTypeUri` as the exception middleware above if it's registered.

## Contributing

Issues and pull requests are welcome:
- Keep changes focused, with a clear description of the behavior change.
- Match the existing code style (see `.editorconfig`).
- Call out any breaking changes to the public API in your PR description.

## License

MIT — see [LICENSE.txt](LICENSE.txt).
