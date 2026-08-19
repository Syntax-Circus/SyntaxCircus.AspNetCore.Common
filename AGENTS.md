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

## Search indexing

```csharp
builder.Services.AddSearchIndexing(builder.Configuration); // binds "SearchIndexing" section
var app = builder.Build();
app.UseSearchIndexingHeaders(); // reads BlockPageMetadata per request
```

| Member | Signature | Behavior |
| --- | --- | --- |
| `AddSearchIndexing` | `IServiceCollection AddSearchIndexing(this IServiceCollection services, IConfiguration configuration)` | Binds `SearchIndexingOptions` from the `"SearchIndexing"` config section (`SearchIndexingOptions.SectionName`). |
| `UseSearchIndexingHeaders` (parameterless) | `IApplicationBuilder UseSearchIndexingHeaders(this IApplicationBuilder app)` | Writes `options.RobotsDirective` when `options.BlockPageMetadata` is `true` and the request path isn't excluded (hardcoded `/robots.txt`/`/sitemap.xml` plus `options.ExcludedPaths`). Requires `AddSearchIndexing` (or another registration of `IOptions<SearchIndexingOptions>`) to have run first. |
| `UseSearchIndexingHeaders` (predicate) | `IApplicationBuilder UseSearchIndexingHeaders(this IApplicationBuilder app, Func<HttpContext, bool> shouldApply)` | On `Response.OnStarting`, sets `X-Robots-Tag` to the **fixed** `SearchIndexingOptions.NoIndexDirective` value **only if** `shouldApply(context)` is `true` **and** the response doesn't already have an `X-Robots-Tag` header. Never overwrites an existing value, and never reads `RobotsDirective` — that's the parameterless overload's job only. Use this overload directly when the condition isn't a static config flag (per-host, per-environment, etc.). |
| `SearchIndexingOptions.BlockPageMetadata` | `bool` (default `false`) | Consulted only by the parameterless `UseSearchIndexingHeaders()` overload. |
| `SearchIndexingOptions.RobotsDirective` | `string` (default `NoIndexDirective`) | The literal value the parameterless overload writes to `X-Robots-Tag`. Settable independently of `NoIndexDirective` — e.g. `"noindex"` alone, or `"noindex,nofollow,noarchive"`. **Not** consulted by the predicate overload. |
| `SearchIndexingOptions.BlockRobotsAndSitemap` | `bool` (default `false`) | Consulted only by `MapRobotsTxt`/`MapSitemap` (see [Search discovery endpoints](#search-discovery-endpoints)). Not read by `UseSearchIndexingHeaders` at all. |
| `SearchIndexingOptions.ExcludedPaths` | `IReadOnlyList<string>` (default `[]`) | Consulted only by the parameterless `UseSearchIndexingHeaders()` overload — **additive** on top of a hardcoded, always-on exclusion of `/robots.txt` and `/sitemap.xml` (see gotcha below on why it defaults empty instead of pre-populated). |
| `SearchIndexingOptions.SectionName` | `const string` = `"SearchIndexing"` | Config section bound by `AddSearchIndexing`. |
| `SearchIndexingOptions.NoIndexDirective` | `const string` = `"noindex,nofollow"` | The value the predicate overload always writes, and `RobotsDirective`'s default. |
| `SearchIndexingOptions.DisallowAllRobotsTxt` | `const string` = `"User-agent: *\nDisallow: /"` | The body `MapRobotsTxt` writes when `BlockRobotsAndSitemap` is `true`. |

Both public overloads share a private `UseRobotsTagHeader(Func<HttpContext, string?> valueSelector)`
core (`null`/empty return = don't set) — the `OnStarting`/`ContainsKey` mechanics live in exactly
one place. Don't reintroduce per-overload duplication of that logic; add new behavior by changing
what a `valueSelector` returns, not by touching the `OnStarting` plumbing itself.

**Gotcha for agents touching `ExcludedPaths`:** it defaults to `[]`, not to
`["/robots.txt", "/sitemap.xml"]`, even though those two paths are always skipped. This is
deliberate, not an oversight — `Microsoft.Extensions.Configuration`'s binder does not *replace* a
pre-populated `IReadOnlyList<T>`/`List<T>` default when binding indexed keys (`ExcludedPaths:0`
etc.); it **appends** to whatever the C# default already contains (verified empirically: binding
`ExcludedPaths:0 = "/health"` against a `["/robots.txt", "/sitemap.xml"]` default produces a
3-element list, not a 1-element list). A pre-populated default would make `ExcludedPaths` from
config permanently un-shrinkable. So the two conventional paths are hardcoded directly in
`SearchIndexingHeaderExtensions.UseSearchIndexingHeaders()` instead, and the config-bound list
starts empty like every other list option in this package (`TrustedProxyOptions.TrustedProxies`,
`SecurityHeadersOptions.PathOverrides`, etc.) and is purely additive. Don't "fix" this by giving
`ExcludedPaths` a non-empty default — it reintroduces the same footgun.

**Gotcha for agents combining this with security headers:** both `UseSecurityHeaders` and
`UseSearchIndexingHeaders` write `X-Robots-Tag` from a `Response.OnStarting` callback, and
`HttpResponse.OnStarting` callbacks run **last-registered-first** (a stack, not a queue).
`UseSecurityHeaders`'s callback sets `X-Robots-Tag` **unconditionally** whenever
`SecurityHeadersOptions.RobotsTag` (or a matching `PathOverride.RobotsTag`) is non-empty for that
path — it does not check for an existing header. `UseSearchIndexingHeaders`'s callback checks
`ContainsKey` first and never overwrites. Working through both registration orders: if
`UseSecurityHeaders` runs later in the pipeline than `UseSearchIndexingHeaders`, its callback
fires *first* (LIFO) and sets the header unconditionally, so `UseSearchIndexingHeaders`'s callback
then sees it already set and backs off. If `UseSecurityHeaders` runs *earlier* in the pipeline,
its callback fires *second* and overwrites whatever `UseSearchIndexingHeaders` just set. Either
way, **a configured `SecurityHeadersOptions.RobotsTag`/`PathOverride` for a path always wins over
`UseSearchIndexingHeaders`'s `noindex,nofollow`**, independent of `Use...` call order. Don't "fix"
an unexpected `X-Robots-Tag` value by reordering the `Use...` calls — it won't change the outcome
when `SecurityHeaders` has `RobotsTag` configured for that path; unset it there instead if
`SearchIndexingHeaders` is meant to control that path.

## Search discovery endpoints

```csharp
app.MapRobotsTxt(_ => "User-agent: *\nAllow: /\nSitemap: https://example.com/sitemap.xml");
app.MapSitemap(_ => [new SitemapEntry("https://example.com/", LastModified: DateOnly.FromDateTime(DateTime.UtcNow))]);
```

Namespace: `SyntaxCircus.AspNetCore.Common`, file `SearchDiscoveryEndpointExtensions.cs`.

| Member | Signature | Behavior |
| --- | --- | --- |
| `MapRobotsTxt` (sync) | `IEndpointRouteBuilder MapRobotsTxt(this IEndpointRouteBuilder endpoints, Func<HttpContext, string> contentFactory, string route = "/robots.txt", TimeSpan? cacheDuration = null)` | Thin wrapper — calls the async overload with `context => Task.FromResult(contentFactory(context))`. |
| `MapRobotsTxt` (async) | `IEndpointRouteBuilder MapRobotsTxt(this IEndpointRouteBuilder endpoints, Func<HttpContext, Task<string>> contentFactory, string route = "/robots.txt", TimeSpan? cacheDuration = null)` | Maps a `text/plain` GET at `route`. If `IOptions<SearchIndexingOptions>.Value.BlockRobotsAndSitemap` is `true`, writes `SearchIndexingOptions.DisallowAllRobotsTxt` and **does not invoke** `contentFactory`; otherwise awaits `contentFactory(context)` and writes it verbatim (no escaping/transformation — caller owns the full body, including any `Sitemap:` line). `cacheDuration`, when set, writes `Cache-Control: public, max-age=<seconds>` before the body — applied regardless of blocked/unblocked, since this endpoint always returns `200`. Mapped `.AllowAnonymous()`. |
| `MapSitemap` (sync) | `IEndpointRouteBuilder MapSitemap(this IEndpointRouteBuilder endpoints, Func<HttpContext, IReadOnlyList<SitemapEntry>> entriesFactory, string route = "/sitemap.xml", TimeSpan? cacheDuration = null)` | Thin wrapper — calls the async overload with `context => Task.FromResult(entriesFactory(context))`. |
| `MapSitemap` (async) | `IEndpointRouteBuilder MapSitemap(this IEndpointRouteBuilder endpoints, Func<HttpContext, Task<IReadOnlyList<SitemapEntry>>> entriesFactory, string route = "/sitemap.xml", TimeSpan? cacheDuration = null)` | Maps an `application/xml` GET at `route`. If `BlockRobotsAndSitemap` is `true`, returns `404` and **does not invoke** `entriesFactory` or apply `cacheDuration` (a blocked sitemap's `404` is intentionally never cache-controlled — avoids a CDN pinning a stale `404` across a `BlockRobotsAndSitemap` flip); otherwise awaits `entriesFactory(context)` and builds a sitemap-protocol XML document (`http://www.sitemaps.org/schemas/sitemap/0.9`) via `System.Xml.Linq` (`<loc>` XML-escaped automatically — unlike hand-rolled string interpolation). Mapped `.AllowAnonymous()`. |
| `SitemapEntry` | `sealed record SitemapEntry(string Url, DateOnly? LastModified = null, SitemapChangeFrequency? ChangeFrequency = null, double? Priority = null)` | One `<url>` entry. `LastModified` → `<lastmod>yyyy-MM-dd</lastmod>` (invariant culture); `ChangeFrequency` → `<changefreq>` as the lowercased enum name (matches protocol values exactly: `always`/`hourly`/`daily`/`weekly`/`monthly`/`yearly`/`never`); `Priority` → `<priority>` via plain invariant `ToString()` (**no range validation** — trust the caller, consistent with the rest of this package). All three omitted from the XML when `null`. |
| `SitemapChangeFrequency` | `enum { Always, Hourly, Daily, Weekly, Monthly, Yearly, Never }` | Sitemap-protocol `<changefreq>` values. |

Both factory delegates are invoked **per-request** (not cached at startup) and both read
`IOptions<SearchIndexingOptions>` fresh per request too — `BlockRobotsAndSitemap` can flip via a
reloadable config provider without an app restart. Neither requires `AddSearchIndexing` to have
been called: `IOptions<T>` resolves to `new SearchIndexingOptions()` defaults regardless, since
ASP.NET Core's default hosting registers the `IOptions<T>` infrastructure unconditionally.

**Gotcha:** `MapRobotsTxt`'s `contentFactory` is responsible for the *entire* body when not
blocked, including any `Sitemap:` line pointing at wherever `MapSitemap` is mapped — the two
methods are intentionally decoupled (no shared route/base-URL wiring) so don't assume one knows
about the other; wire the URL together yourself in the closure passed to `MapRobotsTxt`.

**Gotcha for agents adding a null-arg test:** with both a sync and an async overload sharing a
name, a bare `null!` single-argument call (e.g. `app.MapRobotsTxt(null!)`) is **ambiguous at
compile time** (CS0121) — there's no lambda for the compiler to infer a return type from. Cast to
the specific delegate type in test code: `app.MapRobotsTxt((Func<HttpContext, string>)null!)` vs.
`app.MapRobotsTxt((Func<HttpContext, Task<string>>)null!)`. Real call sites with actual lambdas
never hit this — `ctx => "text"` vs. `async ctx => await ...` resolve unambiguously via
return-type inference.

## Canonical host redirect

```csharp
builder.Services.AddCanonicalHostRedirect(builder.Configuration); // binds "CanonicalHost" section
var app = builder.Build();
app.UseCanonicalHostRedirect(); // early in the pipeline — before routing/auth
```

Namespace: `SyntaxCircus.AspNetCore.Common`, files `CanonicalHostOptions.cs`/`CanonicalHostExtensions.cs`.

| Member | Signature | Behavior |
| --- | --- | --- |
| `AddCanonicalHostRedirect` | `IServiceCollection AddCanonicalHostRedirect(this IServiceCollection services, IConfiguration configuration)` | Binds `CanonicalHostOptions` from the `"CanonicalHost"` section (`CanonicalHostOptions.SectionName`). |
| `UseCanonicalHostRedirect` | `IApplicationBuilder UseCanonicalHostRedirect(this IApplicationBuilder app)` | If `context.Request.Host.Host` case-insensitively matches an entry in `CanonicalHostOptions.LegacyHosts` **and** `CanonicalHostOptions.CanonicalHost` is set, issues a redirect (permanence per `Permanent`) to the same path/query on `CanonicalHost`, scheme per `ForceHttps`, and returns without calling `next()` — the rest of the pipeline never runs for that request. Otherwise calls `next()` normally. |
| `CanonicalHostOptions.CanonicalHost` | `string?` (default `null`) | Target hostname, no scheme/port. `null` makes the middleware a permanent no-op — safe to register unconditionally. |
| `CanonicalHostOptions.LegacyHosts` | `IReadOnlyList<string>` (default `[]`) | Hostnames (no scheme/port) that trigger the redirect. |
| `CanonicalHostOptions.ForceHttps` | `bool` (default `false`) | `false` (default) preserves `context.Request.Scheme`. `true` hardcodes `"https"` for the redirect target — only for requests that are already matching a `LegacyHosts` entry; a canonical-host request on plain `http` is untouched by this middleware either way (that's `UseHsts`/`UseHttpsRedirection`'s job). |
| `CanonicalHostOptions.Permanent` | `bool` (default `true`) | `true` → `Response.Redirect(url, permanent: true)` (301). `false` → 302, for a reversible staged rollout. |

**Scheme handling is scoped narrowly:** `ForceHttps` only fires as part of a `LegacyHosts` match —
it is not a general "redirect everything to https" switch, and adding one here would duplicate
`UseHsts`/`UseHttpsRedirection`'s job. Don't extend this middleware to also upgrade scheme for
already-canonical-host requests; that's a separate, orthogonal concern that belongs in the
HTTPS-redirection layer, not here.

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
