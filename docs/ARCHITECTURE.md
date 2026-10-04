# Architecture

**English** · [Türkçe](ARCHITECTURE.tr.md)

How Elevare is put together, and why. This is the document to read before changing
anything structural; [CONTRIBUTING.md](../CONTRIBUTING.md) covers the mechanics of
getting a change merged.

## Two applications, one database

```
┌──────────────┐   writes    ┌─────────────┐    reads    ┌──────────────┐
│  CMS (Blazor)│ ──────────► │ PostgreSQL  │ ◄────────── │ Site (MVC)   │
│  admin panel │             │  ElevareDB  │             │ public pages │
└──────────────┘             └─────────────┘             └──────────────┘
```

They are separate processes that share a database rather than an API. The rule that
keeps that honest: **the CMS owns the schema and writes; the site reads.** The site
runs no migrations and has no write path into content — its only writes are
append-only telemetry (page views, clicks), form submissions and client error logs.

The site models the CMS's tables with its own read-only entities under
`Domain/Entities/Public*`, deliberately narrower than the CMS's. Two
`PublicSchemaContractTests` suites — one in each module — assert that those
projections still match the CMS's schema, so a column rename in the CMS fails the
build instead of silently breaking the site at runtime.

## Layers

Each application is the same four layers, referenced inwards only:

```
Presentation  ──►  Infrastructure  ──►  Application  ──►  Domain
                                              └──────────►  SharedKernel
```

**`SharedKernel`** — the `Result` / `Error` types both modules build on, domain
abstractions, and small extension helpers. No framework dependencies.

**`Domain`** — entities and the rules that belong to them, with no outward
dependencies at all. `BaseEntity` carries the audit trail (create/update/delete
timestamps and user ids, `IsDeleted`, `IsActive`) that the persistence layer fills
in automatically. Error *messages* live here too, as `Error` constants next to the
entity they describe, so a handler never invents a message inline.

**`Application`** — one class per use case, CQRS-style: `ICommand` / `IQuery` plus
their handlers, dispatched by MediatR. Requests pass through pipeline behaviours in
this order, and the order matters:

1. `DbConcurrencyGuardPipelineBehavior` — serialises access to the scoped `DbContext`.
   A Blazor Server circuit can fire several requests at once from one page, and EF's
   context is not thread-safe; one semaphore per DI scope is what stops that.
2. `RequestLoggingPipelineBehavior` — records every request and its outcome
3. `PermissionPipelineBehavior` — enforces `IRequirePermission` before the handler runs
4. `ValidationPipelineBehavior` — runs the FluentValidation rules
5. `PublicSiteCacheInvalidationPipelineBehavior` — after a command succeeds (and so
   after step 6 has committed it), asks the public site to drop its rendered pages;
   see [Public site caching](#public-site-caching)
6. `SaveChangesPipelineBehavior` — commits once, after the handler returns success

Because the last step exists, most command handlers stage changes and return; they do not
call `SaveChangesAsync` themselves. The exceptions are handlers that need the
generated identity in their response, which save explicitly and leave the behaviour
a harmless no-op.

**`Infrastructure`** — everything that talks to the outside world: JWT issuing and
the session-to-header middleware, SMTP with retries, file storage (local or
S3-compatible), Hangfire jobs, and `Persistence` with the `DbContext`, Identity and
migrations. Redis (the site's page/query cache) is behind its own `IRedisConnection`
wrapper (`Infrastructure/Caching/RedisConnection.cs`) that connects lazily and never
throws — an unreachable server degrades to "no cache" instead of taking the whole
site down with it. A plain `ConnectionMultiplexer.Connect` on an unreachable server
either throws (every request 500s) or, with `AbortOnConnectFail=false` alone, blocks
each command until its own timeout (every request pays that timeout in full); this
wrapper's `TryGetConnected` returns `null` instead of a multiplexer whenever the
connection is not actually live, so the caller treats it as a plain cache miss.

**`Presentation`** — the Blazor admin panel and the MVC site.

## Errors do not travel as exceptions

Handlers return `Result` / `Result<T>`. A failure carries an `Error` with a stable
code such as `PageInfo.TranslationAlreadyExists`, and the UI turns that code into a
sentence through `CmsLocalizer.Error`, which looks it up in `ErrorMessages.resx` /
`ErrorMessages.tr.resx`.

This is why an error code is not a cosmetic detail: it is the translation key. A
code with no resource entry falls back to the English `Error.Description`, which is
how a Turkish user ends up reading English. `ErrorMessageCoverageTests` fails the
build if any declared code has no Turkish message.

Exceptions are still exceptions — genuinely exceptional conditions, caught by the
global handler and turned into a 500. The public site adds `NotFoundException`,
which its middleware renders as a real 404 page in the visitor's language.

The same rule applies to anything else a user reads. No layer below Presentation
produces finished prose: validators carry an error code, and the structured-data
validator returns a key plus its arguments rather than a sentence. It used to return
Turkish text, which meant the domain layer decided what language the editor read —
and an English-speaking editor got Turkish SEO advice.

## Request pipelines

The order is load-bearing in both apps. Current CMS pipeline
(`src/cms/Presentation/Wasm/Wasm/Program.cs`):

1. `UseWebAssemblyDebugging` (development) / `UseExceptionHandler` + `UseHsts` (production)
2. `UseForwardedHeaders` (`X-Forwarded-For`/`-Proto`) — behind nginx or any reverse proxy, every request otherwise looks like plain HTTP from 127.0.0.1, which breaks HTTPS detection and secure cookies; must run before anything below reads `Request.Scheme`
3. `SecurityHeadersMiddleware` — early, so every response including errors carries the headers
4. `UseHttpsRedirection`, `UseStaticFiles`
5. Culture auto-detection from `Accept-Language`, then `UseRequestLocalization`
6. `UseSession`, then `JwtFromSessionMiddleware` — lifts the JWT out of the session into an `Authorization` header
7. `UseRateLimiter`, `UseAuthentication`, `UseAuthorization`, `UseAntiforgery`
8. `MapStaticAssets` (anonymous — a fallback auth policy would otherwise block Blazor's own framework files), `MapRazorComponents`
9. Migrate and seed the database — **before** the Hangfire registrations below, which cannot create a database they need to write to
10. Hangfire dashboard and recurring jobs, auth/media/culture endpoints, `/health`

Public site (`src/web/Presentation/WebMvc/Program.cs`):

1. `UseForwardedHeaders` — same reason as the CMS above; first, since even exception handling downstream needs the real scheme
2. `GlobalExceptionHandlerMiddleware` — so it sees everything after that
3. `SecurityHeadersMiddleware`
4. `UseHsts`, `UseRewriter(SeoRedirectRule)`, `UseHttpsRedirection` (production only) — the rewrite rule canonicalizes host (`www.`) and path casing and enforces HTTPS at the URL level; it skips any request path that has a file extension, so static assets never get rewritten or redirected, only real page slugs do
5. `UseResponseCompression` (Brotli + gzip), `UseWebOptimizer`, `UseStaticFiles` with long cache headers (`public,max-age=31536000,immutable`)
6. `MaintenanceModeMiddleware` — after static files, so the maintenance page's own assets still load; `/health` is exempt
7. `UseRouting`, `UseAuthorization`
8. `UseOutputCache` — after routing, because `[OutputCache]` is endpoint metadata: placed before `UseRouting` it sees no endpoint, finds no policy and caches nothing (which is how it stood until it was moved)
9. `UseWebMarkupMin` (HTML minification) — behind the output cache, so the cached copy is the minified one and a hit skips the minifier
10. `UseRateLimiter` — after the output cache, so a cached page never spends a request budget
11. API endpoints (tracking, logging, forms, search, cache), then `MapDynamicControllerRoute` for `/{language}/{slug}`, then a fallback to the page controller

## Multi-language routing

The default language is served from the bare path (`/hakkimizda`); every other
published language is prefixed (`/en/about-us`); the default language's prefixed
address (`/tr/hakkimizda`) answers with a 301 to the bare one. `SlugRouteValueTransformer` resolves
a path to a page, and `ExistingLanguageRouteConstraint` decides whether a leading
segment is a language code at all — backed by `ILanguageDirectory`, an in-memory
snapshot refreshed every 30 seconds by `SiteStateRefreshHostedService`.

That refresh is why publishing a language, or switching maintenance mode, takes
effect on the site without a restart. The cache-clear call below also refreshes both
snapshots on the spot, so in practice a change made in the CMS lands within a second
or two rather than waiting for the next 30-second tick.

Changing the default language changes every page's address. The CMS writes
redirects from the old addresses to the new ones, rewrites the old addresses in page
content, templates, SEO fields (canonical, `og:url`, structured data) and site
settings (`SiteAddressMigration`), and rebuilds the sitemap straight away.

## Public site caching

The site caches at two levels:

| Level | What | Lifetime | Where |
| --- | --- | --- | --- |
| Rendered pages | The whole minified HTML response of `PageController.Index`, keyed by path plus the page-listing parameters (`page`, `tag`, `q` and their per-block variants — `ListingQuery`); any other query parameter (`utm_*`, `fbclid`) neither changes the page nor makes a new entry | 1 hour | ASP.NET Core output cache (in the Web process's memory), policy `Pages` |
| Lookups | Page by slug, site settings, site-code snippets | 30 seconds | `ICacheService` — Redis when configured, otherwise in-process memory |

The hour is safe only because pages do not have to expire on their own. After every
successful CMS command, `PublicSiteCacheInvalidationPipelineBehavior` asks
`IPublicSiteCacheInvalidator` to clear the site; calls arriving within a second of each
other (a bulk edit, a save made of several commands) go out as one
`POST /api/cache/clear`. That endpoint, in order, empties `ICacheService`, refreshes
the language and maintenance snapshots, and only then evicts the `pages` output-cache
tag — so no request can slip in between and re-cache a page built from stale data.

It is deliberately every command rather than a list of content-changing ones: a page
is built from pages, templates, media, site settings, site codes, languages, redirects
and more, and a list would miss the next feature to be added. A command that changes
nothing public costs one extra re-render. If the call cannot get through, the CMS logs
a warning and the hour is the ceiling on staleness. Changes that bypass the CMS — a
database restore, a manual SQL edit — need the "Clear cache" button on
`/admin/cache`.

A language is only routable when it is **both** active and published; see the
Languages section of the [README](../README.md) for what those two flags mean.

## Background jobs

Hangfire, with PostgreSQL storage, running inside the CMS process:

| Job | Schedule | What it does |
| --- | --- | --- |
| `sitemap-generation` | every 2 hours | Rebuilds the sitemap XML into `SitemapCaches` |
| `reminder-check` | every minute | Fires due user reminders |
| `database-backup` | Sundays 02:00 UTC | Full backup, then prunes older ones |
| `data-retention` | daily 03:20 UTC | Trims analytics, click and log tables, and purges expired Trash |

Retention runs *after* the weekly backup on purpose: the backup captures the rows
the trim is about to delete.

## Integration secrets and live configuration

SMTP, CAPTCHA and S3/CDN credentials can live in `appsettings.json`/environment
variables (read once at startup, like any other config) **or** in the CMS's
"Sırlar" screen, encrypted in the `IntegrationSecrets` table with the same Data
Protection key ring both apps already share for cookies. Either source works; the
database wins when both have a value.

The database side of that is not just another `IOptions<T>` — a save from "Sırlar"
applies **live, no restart**, in both processes:

- `IntegrationSecretsConfigurationProvider` (`Presentation/*/Configuration/`, one per
  app) is a custom, *reloadable* `IConfigurationProvider` — unlike a one-shot
  `AddInMemoryCollection`, its `ReloadAsync()` re-reads the table and fires the
  configuration system's own reload token, which is what `IOptionsMonitor<T>`
  watches. Every consumer of `EmailOptions`/`CaptchaOptions`/`ObjectStorageOptions`
  is therefore injected as `IOptionsMonitor<T>`, not `IOptions<T>` — the latter is
  captured once at DI-construction time and would never see the new value.
- `Update`/`ClearIntegrationSecretCommandHandler` call `IIntegrationSecretsReloader`
  directly after committing (the CMS's own snapshot), and — only for a `Captcha:*`
  key, the one value the Web app also reads — `IIntegrationSecretsChangeNotifier`
  additionally calls `POST /api/secrets/reload` on the Web app, over the same
  internal `Cache:ClearSecret`-authenticated channel the cache-clear feature already
  uses to reach across the process boundary (see `CacheClearService`/
  `CacheEndpoints`, the pattern this one mirrors).
- Switching `ObjectStorage:Provider` between local disk and S3 is not just an
  options value — normally that decision is made once, at DI-registration time,
  picking which concrete `IBlobStorage` gets registered. To make *that* live too,
  both `LocalDiskBlobStorage` and `S3BlobStorage` stay registered at all times, and
  `IBlobStorageFactory.Create(IServiceProvider)` picks between them on every call by
  reading the current `Provider` value. The `IServiceProvider` is deliberately a
  parameter, not a field the factory's own (singleton) constructor captured — a
  singleton holding the *root* provider and using it to resolve a `Scoped` service
  (here, `LocalDiskBlobStorage`, which depends on the request's own `DbContext`)
  fails loudly in Development but **silently pins a single instance for the whole
  app's lifetime in Production** (`ValidateScopes` defaults to on/off exactly along
  that split) — worth remembering any time a singleton needs something Scoped.

## Soft delete

Everything deletable is soft-deleted. `SaveChanges` turns a `Remove` into
`IsDeleted = true`, and a global query filter hides those rows from every normal
query. Unique indexes are filtered on `"IsDeleted" = false`, so a deleted row never
blocks a new one from reusing its slug.

Permanent deletion exists in exactly one place, `TrashPurge`, reached through the
Trash screen, "empty trash" and the retention job. It refuses to destroy a page that
holds form submissions or has sub-pages — see the Trash section of the README.

`ICmsApplicationDbContext.RemovePermanently` is the only way to bypass the
soft-delete interceptor, and it is a named method rather than a flag precisely so
that a hard delete cannot be written by accident and is trivial to grep for.

## Page content

Pages are built with GrapesJS. `PageContent` stores three columns: the rendered
HTML, the CSS, and the editor's own project JSON. The public site renders the stored
HTML; the editor rebuilds its component tree from the JSON when it exists and from
the HTML when it does not.

Two consequences worth knowing before touching anything nearby:

- GrapesJS rewrites element ids and classes on every save, so **diffing raw HTML is
  meaningless**. The approval-diff feature compares reader-visible text instead
  (`HtmlTextDiff`, via AngleSharp).
- A page's staged edit lives in `PreviewGjsHtml`, separate from the live `GjsHtml`.
  That separation is what lets an approval workflow hold a change back while the
  published page keeps serving.

So that a page open in two places cannot have one save silently overwrite the
other, the editor sends the fingerprint the page had when it was opened
(`PageFingerprint`). If the page changed elsewhere in the meantime, the save is
refused with `EditedElsewhere` and the user chooses between loading the current
version and deliberately overwriting it. Templates work the same way.
