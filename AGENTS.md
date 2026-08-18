# AGENTS.md

Agent-oriented API reference for `SyntaxCircus.AspNetCore.Common` and its companion package
`SyntaxCircus.AspNetCore.Common.MassTransit`. Dense and lookup-first — full signatures, decision
tables, and gotchas, with no prose you have to skim through to find them. Humans should read
[`README.md`](README.md) instead; this file is not packed into either NuGet package.

Target framework: `net10.0`. Namespace for everything below: `SyntaxCircus.AspNetCore.Common`
(main package) or `SyntaxCircus.AspNetCore.Common.MassTransit` (companion package), as noted per
section.

## Non-breaking guarantee for this revision

The token-bucket rate-limiter helpers documented below (`AddPartitionedTokenBucket`,
`CreateTokenBucketTier`) are new members only. No existing public signature or behavior changed.
Safe to adopt without touching any existing call site.

## Correlation ID

```csharp
builder.Services.AddCorrelationId(); // optionally: options => options.HeaderName = "X-My-Correlation-Id"
var app = builder.Build();
app.UseCorrelationId();
```

| Member | Signature | Behavior |
| --- | --- | --- |
| `AddCorrelationId` | `IServiceCollection AddCorrelationId(this IServiceCollection services, Action<CorrelationIdOptions>? configure = null)` | Registers `CorrelationIdOptions` (default `HeaderName = "X-Correlation-Id"`). |
| `UseCorrelationId` | `IApplicationBuilder UseCorrelationId(this IApplicationBuilder app)` | Adds `CorrelationIdMiddleware`: reads the header if present, else generates a new correlation ID; echoes it on the response header; tags `Activity.Current`; pushes `CorrelationId`/`TraceId`/`SpanId` into the logger scope for the rest of the request. |
| `CorrelationIdOptions.HeaderName` | `string` (default `"X-Correlation-Id"`) | The header name read/written by the middleware and by the MassTransit filters below. |
| `CorrelationContextAccessor.CurrentCorrelationId` | `static string? { get; set; }` | Ambient (`AsyncLocal`) current correlation ID. `null` outside a request/consume scope. |
| `CorrelationContextAccessor.CurrentTraceId` | `static string?` (get-only) | `Activity.Current?.TraceId.ToString()`. |
| `CorrelationContextAccessor.ResolveCorrelationId` | `static string ResolveCorrelationId(string? fallback = null)` | Precedence: `CurrentCorrelationId` → `fallback` → `CurrentTraceId` → new GUID (`"N"` format). Never returns null/empty. |

Use `ResolveCorrelationId` (not the raw accessor) whenever you need *a* correlation ID
unconditionally — e.g. background jobs, hosted services, anything outside the HTTP pipeline.

## Security headers

```csharp
builder.Services.AddSecurityHeaders(builder.Configuration); // binds "SecurityHeaders" section
var app = builder.Build();
app.UseSecurityHeaders();
```

| Member | Signature | Notes |
| --- | --- | --- |
| `AddSecurityHeaders` | `IServiceCollection AddSecurityHeaders(this IServiceCollection services, IConfiguration configuration)` | Binds `SecurityHeadersOptions` from the `"SecurityHeaders"` config section (`SecurityHeadersOptions.SectionName`). |
| `UseSecurityHeaders` | `IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app)` | Writes the headers on every response. |

`SecurityHeadersOptions` (all `init`-only except `PathOverrides`, which is `set` for config binding):

| Property | Default | Header |
| --- | --- | --- |
| `ReferrerPolicy` | `"strict-origin-when-cross-origin"` | `Referrer-Policy` |
| `FrameOptions` | `"DENY"` | `X-Frame-Options` |
| `ContentTypeOptions` | `"nosniff"` | `X-Content-Type-Options` |
| `PermissionsPolicy` | `"camera=(), geolocation=(), microphone=()"` | `Permissions-Policy` |
| `ContentSecurityPolicy` | `"base-uri 'self'; form-action 'self'; frame-ancestors 'none'; upgrade-insecure-requests"` | `Content-Security-Policy` |
| `StrictTransportSecurity` | `"max-age=31536000; includeSubDomains"` | `Strict-Transport-Security` |
| `RobotsTag` | `null` | `X-Robots-Tag` — **omitted entirely** unless set (unlike the others, which always write). |
| `PathOverrides` | `[]` | `IReadOnlyList<SecurityHeadersPathOverride>` — see below. |

`SecurityHeadersPathOverride`: `PathPrefix` (matched via `PathString.StartsWithSegments`),
`ReferrerPolicy`, `RobotsTag` — first matching entry (in list order) wins; unset properties on
the match fall back to the top-level default, not to `null`.

```json
{ "SecurityHeaders": { "PathOverrides": [
  { "PathPrefix": "/public-profile", "ReferrerPolicy": "no-referrer", "RobotsTag": "noindex, nofollow" }
] } }
```

## Exception handling / HSTS bootstrap

| Member | Signature | Behavior |
| --- | --- | --- |
| `UseStandardExceptionHandling` | `IApplicationBuilder UseStandardExceptionHandling(this IApplicationBuilder app, string errorPath = "/error", bool useStatusCodePages = false)` | `UseExceptionHandler(errorPath)` + `UseHsts()`. Both **skipped entirely in `Development`**. `useStatusCodePages: true` also adds a status-code re-execute page. Not wired in automatically — call it yourself; a pure API host behind a TLS-terminating reverse proxy may not need it at all. |

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

| Member | Signature | Notes |
| --- | --- | --- |
| `AddProblemDetailsExceptionHandling` | `IServiceCollection AddProblemDetailsExceptionHandling(this IServiceCollection services, Action<ProblemDetailsMappingOptions>? configure = null)` | Registers `ProblemDetailsMappingOptions`. |
| `UseProblemDetailsExceptionHandling` | `IApplicationBuilder UseProblemDetailsExceptionHandling(this IApplicationBuilder app)` | Adds `ProblemDetailsExceptionMiddleware`: catches unhandled exceptions, writes an RFC 7807 `ProblemDetails` response. |
| `ProblemMapping` | `sealed record ProblemMapping(int StatusCode, string ErrorCode, string? Detail = null)` | Return type of `ExceptionMapper`. |
| `ProblemDetailsMappingOptions.BaseTypeUri` | `string` (default `""`) | `ProblemDetails.Type` = `BuildTypeUri(errorCode)` = `"{BaseTypeUri.TrimEnd('/')}/{errorCode}"`, or just `errorCode` if `BaseTypeUri` is blank. |
| `ProblemDetailsMappingOptions.ExceptionMapper` | `Func<Exception, ProblemMapping>` | Default mapper: `ArgumentException`→400/`bad-request`, `UnauthorizedAccessException`→403/`forbidden`, `KeyNotFoundException`→404/`not-found`, `InvalidOperationException`→409/`conflict`, else 500/`internal-error`. |
| `ProblemDetailsMappingOptions.IncludeExceptionMessageInDetail` | `bool` (default `false`) | **Security-relevant.** When `false` (default), a mapping with `Detail = null` produces `Detail = null` in the response — `ex.Message` is *never* substituted. Set `true` only after verifying your exception messages don't leak internals (SQL, file paths, connection strings) to API clients. |

**Gotcha for agents editing exception handling:** if you write a custom `ExceptionMapper` that
returns `Detail: null` for some case, that is deliberate — the middleware will not backfill
`ex.Message` unless `IncludeExceptionMessageInDetail` is explicitly `true`. Don't "fix" a null
`Detail` by wiring in `ex.Message` directly in the mapper without checking whether that's the
intent; use the options flag instead so the behavior stays centrally controlled.

## Trusted-proxy validation

| Member | Signature | Behavior |
| --- | --- | --- |
| `AddTrustedProxyForwardedHeaders` | `IServiceCollection AddTrustedProxyForwardedHeaders(this IServiceCollection services, IConfiguration configuration)` | Binds `TrustedProxyOptions` from `"TrustedProxy"` section; wires `ForwardedHeadersOptions` (`X-Forwarded-For`/`-Proto`/`-Host`) from `TrustedProxies`/`TrustedNetworks`; registers an `IStartupFilter` that calls `ValidateTrustedProxyConfiguration` automatically at startup — no separate call needed for the check to run. |
| `ValidateTrustedProxyConfiguration` | `void ValidateTrustedProxyConfiguration(this IHostEnvironment environment, TrustedProxyOptions options)` | Throws outside `Development` if both `TrustedProxies` and `TrustedNetworks` are empty **and** `RequireTrustedProxiesInProduction` is `true` (the default). Exposed directly for calling outside the normal startup path, e.g. in a test. |
| `TrustedProxyOptions.TrustedProxies` | `IReadOnlyList<string>` (default `[]`) | Individual proxy IPs, e.g. `"10.0.0.5"`. |
| `TrustedProxyOptions.TrustedNetworks` | `IReadOnlyList<string>` (default `[]`) | CIDR ranges, e.g. `"10.0.0.0/8"`. |
| `TrustedProxyOptions.RequireTrustedProxiesInProduction` | `bool` (default `true`) | Set `false` to allow an empty trust list outside Development (rare — usually a config bug, not an intended state). |

## Health checks

| Member | Signature | Behavior |
| --- | --- | --- |
| `MapStandardHealthChecks` | `IEndpointRouteBuilder MapStandardHealthChecks(this IEndpointRouteBuilder endpoints, string readyTag = "ready", Func<HttpContext, IReadOnlyDictionary<string, object?>>? metadataFactory = null)` | Maps `/health/live` (predicate `_ => false` — no checks run, just confirms the process is up) and `/health/ready` (runs checks tagged `readyTag`). Both rendered via `HealthCheckResponseWriter.WriteJsonAsync`. `metadataFactory`, when supplied, is invoked per-request and its result nested under a `metadata` key in the JSON; omitted entirely when not supplied. |
| `HealthCheckResponseWriter.WriteJsonAsync` | `static Task WriteJsonAsync(HttpContext context, HealthReport report, IReadOnlyDictionary<string, object?>? metadata = null)` | JSON body: overall `status`, total `duration`, per-check `{name, status, duration, description}[]`, optional `metadata`. Reusable directly if you want the same response shape on a custom `HealthCheckOptions.ResponseWriter`. |

Register checks the normal ASP.NET Core way: `services.AddHealthChecks().AddCheck<T>(tags: ["ready"])`.
This package only standardizes the endpoints/response shape, not check registration.

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

All members are `static` extension methods on `RateLimiterOptions` (or plain statics for the
`Create*Tier` factories) in `RateLimiterOptionsExtensions`.

### Decision table: which limiter shape?

| Requirement | Use |
| --- | --- |
| Hard ceiling that resets at a clock boundary (e.g. "60/minute, full reset each minute") | Fixed-window (`AddPerIpFixedWindow` / `AddPerSubjectFixedWindow` / `AddPartitionedFixedWindow`) |
| Smooth, continuously-replenishing steady-state throttle (no burst-then-silence pattern) | Token-bucket (`AddPartitionedTokenBucket`) |
| Both a burst ceiling *and* a steady-state floor at once (defense-in-depth) | Chain a fixed-window tier + a token-bucket tier via `UseChainedGlobalLimiter(CreateFixedWindowTier(...), CreateTokenBucketTier(...))` |
| Independent quotas by more than one dimension (e.g. per-actor AND per-IP, so a leaked token can't bypass IP throttling) | Chain two tiers of the same or different kind, each with its own `partitionKeySelector` |
| Simple partitioning by remote IP or authenticated subject, one policy | `AddPerIpFixedWindow` / `AddPerSubjectFixedWindow` |
| Custom partition key (route, tenant header, claim combination) | `AddPartitionedFixedWindow` / `AddPartitionedTokenBucket` |

### Full signatures

| Method | Signature |
| --- | --- |
| `AddPerIpFixedWindow` | `RateLimiterOptions AddPerIpFixedWindow(this RateLimiterOptions options, string policyName, int permitLimit, TimeSpan window, Action<FixedWindowRateLimiterOptions>? configure = null)` |
| `AddPerSubjectFixedWindow` | `RateLimiterOptions AddPerSubjectFixedWindow(this RateLimiterOptions options, string policyName, int permitLimit, TimeSpan window, Action<FixedWindowRateLimiterOptions>? configure = null)` |
| `AddPartitionedFixedWindow` | `RateLimiterOptions AddPartitionedFixedWindow(this RateLimiterOptions options, string policyName, Func<HttpContext, string?> partitionKeySelector, int permitLimit, TimeSpan window, Action<FixedWindowRateLimiterOptions>? configure = null)` |
| `AddPartitionedTokenBucket` **(new)** | `RateLimiterOptions AddPartitionedTokenBucket(this RateLimiterOptions options, string policyName, Func<HttpContext, string?> partitionKeySelector, int tokenLimit, int tokensPerPeriod, TimeSpan replenishmentPeriod, Action<TokenBucketRateLimiterOptions>? configure = null)` |
| `CreateFixedWindowTier` | `static PartitionedRateLimiter<HttpContext> CreateFixedWindowTier(Func<HttpContext, string?> partitionKeySelector, int permitLimit, TimeSpan window, Func<HttpContext, bool>? isExempt = null, Action<FixedWindowRateLimiterOptions>? configure = null)` |
| `CreateTokenBucketTier` **(new)** | `static PartitionedRateLimiter<HttpContext> CreateTokenBucketTier(Func<HttpContext, string?> partitionKeySelector, int tokenLimit, int tokensPerPeriod, TimeSpan replenishmentPeriod, Func<HttpContext, bool>? isExempt = null, Action<TokenBucketRateLimiterOptions>? configure = null)` |
| `UseChainedGlobalLimiter` | `RateLimiterOptions UseChainedGlobalLimiter(this RateLimiterOptions options, params PartitionedRateLimiter<HttpContext>[] tiers)` — sets `GlobalLimiter`; a request must pass **every** tier. |
| `UseProblemDetailsRejection` | `RateLimiterOptions UseProblemDetailsRejection(this RateLimiterOptions options, string errorCode = "rate-limited")` — sets `OnRejected` to write a `ProblemDetails` 429, including `Retry-After` when the lease exposes `MetadataName.RetryAfter`. |

Partition keys: `AddPerIpFixedWindow` uses `context.Connection.RemoteIpAddress?.ToString() ?? "unknown"`.
`AddPerSubjectFixedWindow` uses the `sub` claim, falling back to `ClaimTypes.NameIdentifier`, falling
back to remote IP, falling back to `"unknown"`. All partition-key selectors are normalized through
a shared helper: `null`/whitespace → `"unknown"`.

Both `Build*Options` helpers set `QueueLimit = 0` by default (reject immediately over the limit,
no queueing) unless overridden via `configure`. `AutoReplenishment` is left at the BCL default
(`true`) for both fixed-window and token-bucket.

### Migrating from raw `System.Threading.RateLimiting`

Before (raw BCL, e.g. a hand-rolled `AgentToolsPolicy` chaining a burst ceiling with a
steady-state throttle):

```csharp
RateLimitPartition.Get(partitionKey, _ => RateLimiter.CreateChained(
    new FixedWindowRateLimiter(new FixedWindowRateLimiterOptions { PermitLimit = 60, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }),
    new TokenBucketRateLimiter(new TokenBucketRateLimiterOptions { TokenLimit = 10, TokensPerPeriod = 10, ReplenishmentPeriod = TimeSpan.FromSeconds(1), QueueLimit = 0 })));
```

After (this package — same semantics, composes with `UseChainedGlobalLimiter`):

```csharp
options.UseChainedGlobalLimiter(
    RateLimiterOptionsExtensions.CreateFixedWindowTier(partitionKeySelector, permitLimit: 60, window: TimeSpan.FromMinutes(1)),
    RateLimiterOptionsExtensions.CreateTokenBucketTier(partitionKeySelector, tokenLimit: 10, tokensPerPeriod: 10, replenishmentPeriod: TimeSpan.FromSeconds(1)));
```

This is a purely additive swap — no new composition mechanism, `UseChainedGlobalLimiter` and
`CreateFixedWindowTier` already existed; `CreateTokenBucketTier` just fills the gap that made
this one migration impossible before.

## MassTransit correlation propagation

Namespace: `SyntaxCircus.AspNetCore.Common.MassTransit`, package
`SyntaxCircus.AspNetCore.Common.MassTransit` (references the main package; install separately).

```csharp
services.AddMassTransit(x =>
{
    x.UseCorrelationIdPropagation();            // registers filters + consume pipeline
    x.UsingRabbitMq((ctx, cfg) =>
    {
        cfg.UseCorrelationIdPropagation(ctx);   // wires publish + send pipeline
        cfg.ConfigureEndpoints(ctx);
    });
});
```

| Member | Signature | Behavior |
| --- | --- | --- |
| `UseCorrelationIdPropagation` (registration) | `IBusRegistrationConfigurator UseCorrelationIdPropagation(this IBusRegistrationConfigurator configurator)` | Call inside `AddMassTransit(x => ...)`. Registers `CorrelationConsumeFilter<>` on every auto-registered receive endpoint's consume pipeline. |
| `UseCorrelationIdPropagation` (bus factory) | `IBusFactoryConfigurator UseCorrelationIdPropagation(this IBusFactoryConfigurator configurator, IRegistrationContext context)` | Call inside the transport configurator lambda (e.g. `UsingRabbitMq((ctx, cfg) => ...)`). Wires `CorrelationPublishFilter<>` and `CorrelationSendFilter<>`. |

**Both overloads are required** — omitting either one silently breaks propagation in that
direction only (e.g. skip the bus-factory overload and outbound messages carry no correlation
header, even though consume-side enrichment still works for messages from elsewhere).

Behavior: publish/send filters stamp the current `CorrelationContextAccessor.CurrentCorrelationId`
(or a fresh one if none is ambient) onto the message header named by `CorrelationIdOptions.HeaderName`.
`CorrelationConsumeFilter<T>` on the consume side reads that header back off, resolves it via
`CorrelationContextAccessor.ResolveCorrelationId(inboundValue)`, sets
`CorrelationContextAccessor.CurrentCorrelationId` for the duration of the consume (restored to
its previous value in a `finally`), and pushes `CorrelationId`/`TraceId`/`SpanId` into the logger
scope — the same enrichment shape `CorrelationIdMiddleware` uses for HTTP requests. A request
that publishes a message keeps one correlation ID through both the request and the eventual
consume.

## Test project conventions (for contributing tests)

- `tests/SyntaxCircus.AspNetCore.Common.Tests` uses `TestServerFactory.Create(services, app)` to
  spin up an in-memory `TestServer` — see any existing test in
  `RateLimiterOptionsExtensionsTests.cs` for the pattern (configure services, configure the app
  pipeline, `server.CreateClient()`, assert on `HttpResponseMessage`).
- Test runner is xUnit v3 on the Microsoft.Testing.Platform runner (`dotnet test`), not VSTest —
  use `--filter-class`/`--filter-method`/`--filter-namespace` to scope a run, not `--filter`.
- Pass `TestContext.Current.CancellationToken` to any async call that accepts one.
