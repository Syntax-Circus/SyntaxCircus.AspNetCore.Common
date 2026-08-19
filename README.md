# SyntaxCircus.AspNetCore.Common

[![Build](https://github.com/Syntax-Circus/SyntaxCircus.AspNetCore.Common/actions/workflows/build.yml/badge.svg)](https://github.com/Syntax-Circus/SyntaxCircus.AspNetCore.Common/actions/workflows/build.yml)
[![NuGet](https://img.shields.io/nuget/v/SyntaxCircus.AspNetCore.Common.svg)](https://www.nuget.org/packages/SyntaxCircus.AspNetCore.Common)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE.txt)

The small pieces of ASP.NET Core host boilerplate that show up in nearly every project, in one place: correlation-ID middleware, security headers, search-indexing opt-out headers with robots.txt/sitemap.xml endpoint helpers, canonical-host redirects, a composable exception-handler/HSTS bootstrap, RFC 7807 ProblemDetails exception handling, trusted-proxy validation, standard health check endpoints, fixed-window/token-bucket rate-limiting policy helpers, and (via the optional `SyntaxCircus.AspNetCore.Common.MassTransit` package) correlation-ID propagation across a MassTransit bus.

> **No support guaranteed.** Published as-is and maintained on a best-effort basis. Issues and PRs are welcome, but there's no SLA — fork it or vendor what you need if that's not enough.

## Correlation ID

```csharp
builder.Services.AddCorrelationId(); // optionally: options => options.HeaderName = "X-My-Correlation-Id"

var app = builder.Build();
app.UseCorrelationId();
```

Reads (or generates) a correlation ID per request, echoes it on the response header, tags the current `Activity`, and pushes it — with the current trace/span ID — into the logger scope for the rest of the request, so downstream log lines carry it automatically. `CorrelationContextAccessor` gives ambient (AsyncLocal) access to it outside the middleware pipeline:

| Member | Purpose |
| --- | --- |
| `CurrentCorrelationId` | The current request's correlation ID (get/set), or `null` outside a request. |
| `CurrentTraceId` | The current `System.Diagnostics.Activity`'s trace ID, or `null` if there is none. |
| `ResolveCorrelationId(fallback = null)` | `CurrentCorrelationId`, else `fallback`, else `CurrentTraceId`, else a new GUID — in that order. Useful for background work (queue consumers, hosted services) that wants *a* correlation ID even outside an HTTP request. |

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

## Search indexing

```csharp
builder.Services.AddSearchIndexing(builder.Configuration); // binds the "SearchIndexing" section

var app = builder.Build();
app.UseSearchIndexingHeaders(); // reads BlockPageMetadata per request
```

Useful for a staging/preview deployment, or a pre-launch site, that should never show up in
search results: when `SearchIndexingOptions.BlockPageMetadata` is `true`, every response gets
`X-Robots-Tag: noindex,nofollow` — unless a response already has an `X-Robots-Tag` header, in
which case this middleware leaves it alone rather than overwriting it. Toggle the whole thing
with one config value instead of hand-rolling middleware per environment:

```json
{ "SearchIndexing": { "BlockPageMetadata": true } }
```

If `BlockPageMetadata` needs to depend on something other than a static flag — e.g. only on a
specific host, or only outside Production — use the predicate overload instead of binding
options at all:

```csharp
app.UseSearchIndexingHeaders(ctx => ctx.Request.Host.Host == "staging.example.com");
```

The parameterless overload never sets `X-Robots-Tag` on `/robots.txt` or `/sitemap.xml` — those
two paths should never carry a noindex signal themselves — even when `BlockPageMetadata` is
`true`. Add more paths to that skip list with `ExcludedPaths` (purely additive; the built-in two
are always skipped regardless of what's configured here):

```json
{ "SearchIndexing": { "BlockPageMetadata": true, "ExcludedPaths": ["/health"] } }
```

`SearchIndexingOptions.BlockRobotsAndSitemap` powers the [search discovery
endpoints](#search-discovery-endpoints-robotstxt--sitemapxml) below — it isn't read anywhere else.

The literal value written is `SearchIndexingOptions.RobotsDirective`, which defaults to
`NoIndexDirective` (`"noindex,nofollow"`) but is a plain settable string — set it to `"noindex"`
alone, or add directives like `"noindex,nofollow,noarchive"`, without needing the predicate
overload:

```json
{ "SearchIndexing": { "BlockPageMetadata": true, "RobotsDirective": "noindex" } }
```

`RobotsDirective` only affects the parameterless overload — `UseSearchIndexingHeaders(shouldApply)`
always writes the fixed `NoIndexDirective` value, regardless of what's configured.

**Interaction with security headers:** if you also use [security headers](#security-headers) and
set `SecurityHeadersOptions.RobotsTag` (or a matching `PathOverride`) for the same path, that
value always wins over `UseSearchIndexingHeaders`'s `noindex,nofollow`, regardless of which
`Use...` call comes first in the pipeline — `UseSecurityHeaders` writes `X-Robots-Tag`
unconditionally, while `UseSearchIndexingHeaders` only writes it when nothing else has already
set it. Don't configure both for the same route expecting the search-indexing value to take
precedence; use `SecurityHeadersOptions.RobotsTag`/`PathOverrides` there instead.

## Search discovery endpoints (robots.txt / sitemap.xml)

```csharp
app.MapRobotsTxt(_ => "User-agent: *\nAllow: /\nSitemap: https://example.com/sitemap.xml");
app.MapSitemap(_ =>
[
    new SitemapEntry("https://example.com/", LastModified: DateOnly.FromDateTime(DateTime.UtcNow)),
    new SitemapEntry("https://example.com/pricing"),
]);
```

Both read `SearchIndexingOptions.BlockRobotsAndSitemap` per request (no `AddSearchIndexing` call
required — `IOptions<T>` resolves to defaults even unconfigured) and short-circuit your supplied
content when it's `true`: `MapRobotsTxt` returns a disallow-all body
(`SearchIndexingOptions.DisallowAllRobotsTxt`) instead of calling `contentFactory`, and
`MapSitemap` returns `404` instead of calling `entriesFactory` — a single flag takes a
staging/preview deployment fully out of both files without touching the app-specific content
itself. `MapSitemap` XML-escapes URLs properly (via `System.Xml.Linq`), and both endpoints are
mapped `.AllowAnonymous()` so a global auth policy elsewhere in the app doesn't block crawlers.
Routes default to `/robots.txt`/`/sitemap.xml` but take an optional second parameter to use
something else.

Both also accept an async factory — useful when the content comes from a database (e.g. a sitemap
built from published blog posts):

```csharp
app.MapSitemap(async ctx =>
{
    var posts = await ctx.RequestServices.GetRequiredService<IBlogRepository>().GetPublishedAsync();
    return posts.Select(p => new SitemapEntry($"https://example.com/blog/{p.Slug}", p.PublishedOn)).ToList();
});
```

`SitemapEntry` also carries the sitemap protocol's other two optional fields —
`ChangeFrequency` (a `SitemapChangeFrequency` enum: `Always`/`Hourly`/`Daily`/`Weekly`/`Monthly`/
`Yearly`/`Never`) and `Priority` (a `double`, conventionally `0.0`–`1.0`) — both omitted from the
XML when left `null`.

Since robots.txt/sitemap.xml rarely change per-request, both methods take an optional
`cacheDuration` to set `Cache-Control: public, max-age=...` (omitted entirely when not passed, and
never applied to `MapSitemap`'s blocked-state `404` so a CDN doesn't cache a stale not-found across
a `BlockRobotsAndSitemap` flip):

```csharp
app.MapRobotsTxt(_ => "User-agent: *\nAllow: /", cacheDuration: TimeSpan.FromHours(1));
```

## Canonical host redirect

```csharp
builder.Services.AddCanonicalHostRedirect(builder.Configuration); // binds the "CanonicalHost" section

var app = builder.Build();
app.UseCanonicalHostRedirect(); // call early — before routing/auth — so legacy hosts short-circuit cheaply
```

301-redirects a configured set of legacy/apex hostnames to one canonical host, preserving the
request's scheme, path, and query string:

```json
{ "CanonicalHost": { "CanonicalHost": "www.example.com", "LegacyHosts": ["example.com", "old.example.com"] } }
```

A no-op until `CanonicalHost` is set (`null` by default) — safe to register unconditionally.
Redirects only host-consolidation by default, not scheme; pair with `UseStandardExceptionHandling`'s
`UseHsts()` (or your reverse proxy) if you also need to force HTTPS on requests that are already on
the canonical host.

For a legacy host on `http`, that means two redirects back-to-back (this middleware, then
`UseHsts`/`UseHttpsRedirection`) unless you opt into `ForceHttps`, which upgrades the scheme in the
same redirect:

```json
{ "CanonicalHost": { "CanonicalHost": "www.example.com", "LegacyHosts": ["example.com"], "ForceHttps": true } }
```

The redirect is a permanent `301` by default; set `Permanent: false` for a `302` during a staged
rollout — reversible while you verify the redirect behaves correctly, then flip back to permanent
once confirmed:

```json
{ "CanonicalHost": { "CanonicalHost": "www.example.com", "LegacyHosts": ["example.com"], "Permanent": false } }
```

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

### Token-bucket (additive, non-breaking)

Fixed-window resets its whole quota at each window boundary — good for a hard burst ceiling,
but it lets a client burn its entire limit in the first instant of a new window. Token-bucket
instead replenishes continuously (`tokensPerPeriod` tokens added every `replenishmentPeriod`),
which suits a steady-state throttle better:

```csharp
builder.Services.AddRateLimiter(options =>
{
    options.AddPartitionedTokenBucket(
        "steady-state",
        ctx => ctx.Connection.RemoteIpAddress?.ToString(),
        tokenLimit: 10,
        tokensPerPeriod: 10,
        replenishmentPeriod: TimeSpan.FromSeconds(1));

    options.UseProblemDetailsRejection();
});
```

| Method | Purpose |
| --- | --- |
| `AddPartitionedTokenBucket(policyName, partitionKeySelector, tokenLimit, tokensPerPeriod, replenishmentPeriod, configure = null)` | Token-bucket policy partitioned by a custom key selector, with optional per-policy `TokenBucketRateLimiterOptions` overrides. |

### Chained multi-tier global limiter (additive, non-breaking)

For defense-in-depth rate limiting — e.g. requiring a request to pass both a per-actor
*and* a per-IP quota, so a leaked token can't bypass IP-level throttling and vice versa —
compose independently-partitioned tiers into `RateLimiterOptions.GlobalLimiter`:

```csharp
builder.Services.AddRateLimiter(options =>
{
    options.UseChainedGlobalLimiter(
        RateLimiterOptionsExtensions.CreateFixedWindowTier(
            ctx => ctx.User.FindFirst("sub")?.Value,
            permitLimit: 600,
            window: TimeSpan.FromMinutes(1),
            isExempt: ctx => ctx.Request.Path.StartsWithSegments("/health")),
        RateLimiterOptionsExtensions.CreateFixedWindowTier(
            ctx => ctx.Connection.RemoteIpAddress?.ToString(),
            permitLimit: 60,
            window: TimeSpan.FromMinutes(1),
            isExempt: ctx => ctx.Request.Path.StartsWithSegments("/health")));

    options.UseProblemDetailsRejection();
});
```

A request must pass every tier's limiter to proceed; `isExempt` bypasses an individual
tier (e.g. for health-check paths) without disabling the others.

Tiers aren't limited to one kind of limiter — chaining a fixed-window burst ceiling with a
token-bucket steady-state throttle is a common defense-in-depth shape for APIs that need both
"no more than 60 requests in any one minute" *and* "no more than ~10 requests per second,
sustained":

```csharp
builder.Services.AddRateLimiter(options =>
{
    options.UseChainedGlobalLimiter(
        RateLimiterOptionsExtensions.CreateFixedWindowTier(
            ctx => ctx.User.FindFirst("sub")?.Value,
            permitLimit: 60,
            window: TimeSpan.FromMinutes(1)),
        RateLimiterOptionsExtensions.CreateTokenBucketTier(
            ctx => ctx.User.FindFirst("sub")?.Value,
            tokenLimit: 10,
            tokensPerPeriod: 10,
            replenishmentPeriod: TimeSpan.FromSeconds(1)));

    options.UseProblemDetailsRejection();
});
```

This is a drop-in replacement for hand-rolling the equivalent with raw BCL types
(`RateLimiter.CreateChained(new FixedWindowRateLimiter(...), new TokenBucketRateLimiter(...))`)
— same semantics, without constructing the limiter options by hand at every call site.

| Method | Purpose |
| --- | --- |
| `CreateFixedWindowTier(partitionKeySelector, permitLimit, window, isExempt = null, configure = null)` | Builds one fixed-window `PartitionedRateLimiter<HttpContext>` tier for chaining. |
| `CreateTokenBucketTier(partitionKeySelector, tokenLimit, tokensPerPeriod, replenishmentPeriod, isExempt = null, configure = null)` | Builds one token-bucket `PartitionedRateLimiter<HttpContext>` tier for chaining. |
| `UseChainedGlobalLimiter(params tiers)` | Sets `GlobalLimiter` to the chained combination of the given tiers. |

## MassTransit correlation propagation

Optional companion package — install `SyntaxCircus.AspNetCore.Common.MassTransit` separately:

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

Both calls are required — they wire different halves of the pipeline. Together, they carry the
ambient correlation ID (the same one `CorrelationContextAccessor`/`CorrelationIdMiddleware`
manage for HTTP requests) across message-bus boundaries: `UseCorrelationIdPropagation(ctx)`
stamps it onto the configured header (`CorrelationIdOptions.HeaderName`) on publish/send, and
`UseCorrelationIdPropagation()` reads it back off inbound messages on consume, sets
`CorrelationContextAccessor.CurrentCorrelationId` for the duration of the consume, and pushes
`CorrelationId`/`TraceId`/`SpanId` into the logger scope — the same enrichment shape as the HTTP
middleware, so a request that triggers a published message keeps one correlation ID through both.

| Method | Purpose |
| --- | --- |
| `UseCorrelationIdPropagation(this IBusRegistrationConfigurator)` | Registers the filter types and wires the consume pipeline for all auto-registered receive endpoints. Call inside `AddMassTransit(x => ...)`. |
| `UseCorrelationIdPropagation(this IBusFactoryConfigurator, IRegistrationContext)` | Wires the publish and send filters. Call inside the transport configurator lambda (e.g. `UsingRabbitMq((ctx, cfg) => ...)`). |

## Contributing

Issues and pull requests are welcome:
- Keep changes focused, with a clear description of the behavior change.
- Match the existing code style (see `.editorconfig`).
- Call out any breaking changes to the public API in your PR description.

## License

MIT — see [LICENSE.txt](LICENSE.txt).
