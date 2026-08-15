# SyntaxCircus.AspNetCore.Common

[![Build](https://github.com/Syntax-Circus/SyntaxCircus.AspNetCore.Common/actions/workflows/build.yml/badge.svg)](https://github.com/Syntax-Circus/SyntaxCircus.AspNetCore.Common/actions/workflows/build.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE.txt)

The small pieces of ASP.NET Core host boilerplate that show up in nearly every project, in one place: correlation-ID middleware, security headers, and a composable exception-handler/HSTS bootstrap.

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

## Contributing

Issues and pull requests are welcome:
- Keep changes focused, with a clear description of the behavior change.
- Match the existing code style (see `.editorconfig`).
- Call out any breaking changes to the public API in your PR description.

## License

MIT — see [LICENSE.txt](LICENSE.txt).
