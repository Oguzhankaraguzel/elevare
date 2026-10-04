# Elevare

**English** · [Türkçe](README.tr.md)

[![CI](https://github.com/Oguzhankaraguzel/elevare/actions/workflows/ci.yml/badge.svg)](https://github.com/Oguzhankaraguzel/elevare/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-9.0-512BD4.svg)](https://dotnet.microsoft.com/)

A .NET 9 content management system and the public web site it publishes to.

The CMS is a Blazor Server admin panel with a GrapesJS page builder, approval
workflows, role-based permissions, multi-language content and scheduled jobs. The
public site is an ASP.NET Core MVC app that renders what the CMS publishes. The two
are separate applications that share one PostgreSQL database: **the CMS owns the
schema and writes; the site reads.**

```
├── src
│   ├── SharedKernel      # Reusable cross-cutting code
│   ├── cms               # CMS application (Core, Infrastructure, Presentation)
│   └── web               # Public site (Core, Infrastructure, Presentation)
├── tests
│   ├── cms               # Tests for the CMS module
│   └── web               # Tests for the web module
└── Elevare.sln
```

## What it does

- **Visual page building** with GrapesJS — drag-and-drop blocks, reusable templates,
  and a preview link you can share before anything is published.
- **Approval workflows**: configurable steps, per-step reviewer roles, and a diff
  that compares reader-visible text rather than the HTML GrapesJS rewrites on
  every save.
- **Role-based permissions** enforced server-side in the request pipeline, editable
  at runtime — hiding a button is never the only thing stopping an action.
- **Multi-language content** with prefix routing, per-language publishing, and a
  translation matrix on the Pages screen.
- **SEO tooling**: per-page meta and structured data, editable `robots.txt` and
  `llms.txt`, a generated sitemap, and redirect management that reports dead targets
  and loops.
- **Social sharing**: the full Open Graph vocabulary per page — every `og:type` with
  its type-specific properties, image/video/audio details, locale — and all four X
  card types, with a preview of the share card in the editor.
- **Images that weigh what they should**: one upload is stored as a master (2560 px
  cap, upright, JPEG at quality 90) with WebP and resized copies beside it, served
  through `<picture>`/`srcset` with a `sizes` hint taken from the page's own CSS;
  a sized favicon set from one library image; a width/height field in the editor
  that keeps the file's proportions, and an SEO check for images shown at the
  wrong ones.
- **Forms** with submissions, canned replies, spam protection and file attachments
  (three files, 10 MB, five types admitted by content; stored privately beside the
  submission, downloadable only with the permission).
- **Tag management** with usage counts, so the tag a typo left behind is visible and
  removable rather than living in the picker forever.
- **Operations**: scheduled backups, data retention, a job dashboard, cache control
  and an audited Trash.

## A look at it

The page builder — drag-and-drop blocks, live SEO score, and a preview link you can
share before anything goes live:

![The page builder](docs/screenshots/page-editor.png)

Redirects, with the diagnosis first. Dead targets, loops and multi-hop chains
announce themselves, because a broken redirect is otherwise invisible until a
visitor hits the 404:

![Redirect management](docs/screenshots/redirects.png)

Permissions are per-role and editable at runtime, and enforced in the request
pipeline rather than by hiding buttons:

![Roles and permissions](docs/screenshots/permissions.png)

Languages carry two separate flags — authorable in the CMS, live on the site — so an
unfinished translation cannot leak, and a finished one is never invisible by accident:

![Language management](docs/screenshots/languages.png)

The Pages list doubles as a translation matrix. A language that is authorable but
not yet published is marked, so a page reading "Published" while being unreachable
says so on the screen where you would notice:

![Pages and translations](docs/screenshots/pages.png)

The editor answers "where do I change this?" the same way every time: one
right-hand column with four tabs — İçerik for what the selected element is,
Görünüm for how it looks, then layers and blocks — and everything about the page
itself — SEO, social cards, structured data, tags, languages, site codes — is one
Page Settings panel. Its utilities open as floating panels you arrange yourself —
a media library, a task board, notes and reminders, and builders for
WhatsApp/campaign links and JSON-LD — so none of them costs you the page you were
working on:

![Editor tools](docs/screenshots/tools.png)

Documentation: [Architecture](docs/ARCHITECTURE.md) ·
[Contributing](CONTRIBUTING.md) · [Security](SECURITY.md) ·
[Changelog](CHANGELOG.md)

There are three ways to run it: **Docker** (quickest), `dotnet run` against a
PostgreSQL you already have, or a folder publish to IIS. Pick whichever fits.

---

## Option A — Docker

Requires Docker Desktop (or Docker Engine + Compose v2). Nothing else: no .NET SDK,
no PostgreSQL install.

```bash
cp .env.example .env
```

Open `.env` and fill in the blanks — the stack refuses to start with any of them
empty, deliberately, so nothing ships with a default password.

| Variable | What it is |
| --- | --- |
| `POSTGRES_PASSWORD` | Password for the `POSTGRES_USER` PostgreSQL role. |
| `JWT_SECRET` | Signs admin sessions. **Minimum 32 characters** — the CMS throws on startup below that. |
| `PREVIEW_SIGNING_KEY` | Signs "preview an unpublished page" links. The CMS and the site must have the **same** value or preview links will not validate. |
| `CACHE_CLEAR_SECRET` | Authorises the CMS's "clear the site cache" calls. Same value on both apps; an empty one makes the site reject every clear request with 401. |
| `ADMIN_EMAIL`, `ADMIN_PASSWORD` | The first administrator, created on the first run against an empty database. |
| `ADMIN_USERNAME` | Defaults to `superadmin`. |

Generate the two keys with whatever you have:

```bash
openssl rand -base64 48
```

```powershell
[Convert]::ToBase64String((1..48 | ForEach-Object { Get-Random -Maximum 256 }))
```

Then:

```bash
docker compose up -d --build
```

The first build takes a few minutes. Once it settles:

| | URL |
| --- | --- |
| CMS (admin) | <http://localhost:5150> |
| Public site | <http://localhost:5160> |

Ports are configurable in `.env` (`CMS_PORT`, `WEB_PORT`, `DB_PORT`, `REDIS_PORT`).

Useful commands:

```bash
docker compose logs -f cms
```

```bash
docker compose down
```

```bash
docker compose down -v
```

`down` stops everything and keeps the data. `down -v` also deletes the volumes —
database, uploaded media, backups and the data-protection keyring — which is how you
get a genuinely clean first run again.

### What compose actually starts

- **db** — PostgreSQL 15 (Alpine). The apps wait for its healthcheck before
  starting, so `up` does not race the database.
- **cache** — Redis. Optional: the CMS picks its cache provider in Site Settings and
  the site works fine without one. It is here so that choice is a setting change
  rather than an infrastructure project.
- **cms** — runs migrations and seeding on startup, then serves the admin panel.
- **web** — the public site.

Volumes: `db-data`, `db-backups`, `cms-uploads`, `cms-keys`. `db-backups` is kept
separate from `db-data` on purpose — see [Configuration](#configuration) — and is
mounted only into `cms`, since (unlike SQL Server) PostgreSQL has no in-database
backup statement — `DatabaseBackupService` shells out to `pg_dump` from the CMS
container itself.

### Local dev tools (optional)

`docker-compose.dev-tools.yml` layers two containers on top of the stack for things
that would otherwise need a real external service. **Development only** — neither is
configured for anything else, and the Seq one deliberately runs with no
authentication at all.

```bash
docker compose -f docker-compose.yml -f docker-compose.dev-tools.yml up -d
```

| | What it gives you | Where |
| --- | --- | --- |
| **Mailpit** | Catches every email the CMS sends, so SMTP and the Site Settings From-address override can be tested without a mailbox. | <http://localhost:8025> |
| **Seq** | Both apps' structured logs, searchable by field rather than grepped — `Application = 'Elevare.Web' and RequestPath like '/urun%'`. Every event carries `Application`, so the two apps stay apart in one place. | <http://localhost:5341> |

Ports are `MAILPIT_UI_PORT` and `SEQ_UI_PORT` in `.env`. Seq keeps its history in a
`seq-data` volume, so a rebuild does not wipe what you were reading. Leaving the
extra file off (`docker compose up -d`) puts both apps back on the console sink and
stops the containers.

---

## Option B — without Docker

Requires the [.NET 9 SDK](https://dotnet.microsoft.com/download) and a PostgreSQL
instance you can reach (local install, Docker, or a remote one).

**1. Point the apps at your database.** Both `appsettings.json` files ship with

```
Host=localhost;Port=5432;Database=ElevareDB;Username=postgres;Password=postgres
```

which works as-is against a default local install. For anything else, override it —
see [Configuration](#configuration) below for how.

**2. Set the secrets.** At minimum `Jwt:SecretKey` (32+ chars), `Preview:SigningKey`
and `Cache:ClearSecret` (each the same value in both apps), and the first
administrator.

The quickest route is to copy the two example files next to the apps:

```bash
cp src/cms/Presentation/Wasm/Wasm/appsettings.Development.json.example src/cms/Presentation/Wasm/Wasm/appsettings.Development.json
```

```bash
cp src/web/Presentation/WebMvc/appsettings.Development.json.example src/web/Presentation/WebMvc/appsettings.Development.json
```

Both copies are git-ignored, so what you put in them stays on your machine. If you
would rather keep secrets off disk entirely, `dotnet user-secrets` does the same job:

```bash
dotnet user-secrets --project src/cms/Presentation/Wasm/Wasm set "Jwt:SecretKey" "<32+ characters>"
```

```bash
dotnet user-secrets --project src/cms/Presentation/Wasm/Wasm set "Seed:SuperAdmin:UserName" "superadmin"
```

```bash
dotnet user-secrets --project src/cms/Presentation/Wasm/Wasm set "Seed:SuperAdmin:Email" "you@example.com"
```

```bash
dotnet user-secrets --project src/cms/Presentation/Wasm/Wasm set "Seed:SuperAdmin:Password" "<your password>"
```

```bash
dotnet user-secrets --project src/web/Presentation/WebMvc set "Preview:SigningKey" "<same value as the CMS>"
```

**3. Run.** The CMS creates the database and applies migrations itself on startup —
there is no separate migration step.

```bash
dotnet run --project src/cms/Presentation/Wasm/Wasm/Wasm.csproj --launch-profile "CMS (https)"
```

```bash
dotnet run --project src/web/Presentation/WebMvc/WebMvc.csproj --launch-profile "Web (https)"
```

In Visual Studio, the **Elevare (CMS + Web)** launch profile starts both at once.

| Application | HTTPS | HTTP |
| --- | --- | --- |
| CMS (admin) | `https://localhost:7150` | `http://localhost:5150` |
| Public site | `https://localhost:7160` | `http://localhost:5160` |

### "Address already in use"

A debug session that was killed rather than stopped keeps holding the port:

```powershell
Get-Process Wasm,WebMvc -ErrorAction SilentlyContinue | Stop-Process -Force
```

---

## Option C — folder publish to IIS

Neither Docker nor `dotnet run`: publish each app to a folder and let IIS host it.
The projects generate their own `web.config`, so nothing extra is needed to make IIS
pick them up.

```bash
dotnet publish src/cms/Presentation/Wasm/Wasm/Wasm.csproj -c Release -o C:\inetpub\elevare-cms
```

```bash
dotnet publish src/web/Presentation/WebMvc/WebMvc.csproj -c Release -o C:\inetpub\elevare-web
```

The server needs the [.NET 9 Hosting Bundle](https://dotnet.microsoft.com/download/dotnet/9.0)
— the plain runtime is not enough, because IIS needs the ASP.NET Core Module that
ships with the bundle.

`appsettings.Development.json` is deliberately excluded from the publish output, so
your local connection string, signing keys and first-run password never travel to a
server.

### Configuration

Everything under [Configuration](#configuration) applies. Supply it as environment
variables on the app pool, or in `web.config`:

```xml
<aspNetCore processPath="dotnet" arguments=".\Wasm.dll" hostingModel="inprocess">
  <environmentVariables>
    <environmentVariable name="ASPNETCORE_ENVIRONMENT" value="Production" />
    <environmentVariable name="ConnectionStrings__DefaultConnection" value="Host=localhost;Port=5432;Database=ElevareDB;Username=elevare;Password=..." />
    <environmentVariable name="Jwt__SecretKey" value="..." />
    <environmentVariable name="Preview__SigningKey" value="..." />
    <environmentVariable name="Cache__ClearSecret" value="..." />
    <environmentVariable name="DataProtection__KeyPath" value="C:\inetpub\elevare-keys" />
  </environmentVariables>
</aspNetCore>
```

Unlike SQL Server's `Trusted_Connection`, PostgreSQL authenticates with the username
and password in the connection string itself — there is no Windows-identity
shortcut, so the role above needs its own login and rights on the database.

### Three things that will otherwise bite you

**Set `DataProtection:KeyPath`.** Without it the encryption keys land in the user
profile, and an app pool that loads no profile keeps them in memory instead: every
recycle signs every admin out and invalidates the antiforgery token on any page
already open. Point it at a folder the app pool identity can write to.

**Set the app pool to run continuously, or the scheduled jobs stop.** Hangfire runs
inside the CMS process. With the default idle time-out, IIS shuts the app down after
20 minutes without a request — and until someone next opens the panel, nothing
generates sitemaps, sends reminders, takes backups or trims data. In the app pool's
Advanced Settings set **Start Mode** to `AlwaysRunning` and **Idle Time-out** to `0`,
then enable the IIS *Application Initialization* feature and `preloadEnabled="true"`
on the site.

**Give the app pool identity write access** to the CMS's `wwwroot\uploads`, to the
`DataProtection:KeyPath` folder, and to `Backup:Directory` (or wherever the default
under the app's own folder resolves to). Unlike SQL Server, PostgreSQL has no
in-database `BACKUP DATABASE` statement — `DatabaseBackupService` shells out to
`pg_dump`/`pg_restore` from the app process itself, so it is the **app pool
identity**, not a database service account, that needs the `postgresql-client` tools
on PATH and write access to that folder.

Leaving `Backup:Directory` empty falls back to a folder next to the running app —
fine for a quick check, but worth pointing at a different disk (or a network share)
than wherever PostgreSQL's own data directory lives, so losing one disk doesn't take
the database and every backup of it down together.

### Known limitation

Each app expects to be **the root of its own site**. Hosting the CMS in a virtual
directory (`https://example.com/cms`) does not work: its links are root-absolute and
would resolve against the domain root instead. Give each app its own site or
subdomain.

---

## Deploying on every push

A push to `master` can deploy itself: `.github/workflows/ci.yml` waits for the tests,
the advisory scan and both image builds to pass, then connects to the server and
does exactly what you would have typed there.

```bash
git fetch origin master
git reset --hard origin/master
docker compose up -d --build
docker image prune -f
```

The step is off until you turn it on, so a fork never tries to reach a server it
does not have. Under *Settings → Secrets and variables → Actions*, add two
**variables** — `DEPLOY_ENABLED` set to `true`, and `DEPLOY_PATH`, the folder of the
checkout on the server — and four **secrets**:

| Name | What it is |
| --- | --- |
| `DEPLOY_HOST` | The server's hostname or IP. |
| `DEPLOY_USER` | The SSH user. It has to be in the `docker` group and own the checkout. |
| `DEPLOY_SSH_KEY` | A private key whose public half is in that user's `~/.ssh/authorized_keys`. Generate a new one for this rather than reusing your own. |
| `DEPLOY_PORT` | The SSH port. |

Two consequences worth knowing before the first run:

- **`git reset --hard` discards changes made on the server** to anything tracked by
  git. `.env` is not tracked, so it survives; a config file you edited in place does
  not.
- **The server builds the images.** That is minutes of CPU per deploy, most of it the
  CMS's Blazor WebAssembly client. If that becomes a problem, the fix is to build in
  CI and have the server pull instead — a bigger change, worth making only once the
  build time actually hurts.

---

## First run

Whichever route you took, the first startup against an empty database will:

1. create the database and apply the single `InitialCreate` migration,
2. seed the roles (SuperAdmin, Admin, Editor, Author, Viewer, Developer),
3. create the administrator **only if all three of** `Seed:SuperAdmin:UserName`,
   `Email` and `Password` are configured — a half-filled section creates nothing, on
   purpose, so a typo cannot produce an account with a password you did not choose,
4. seed the two languages, the site settings catalogue, the maintenance page and
   three starter form-reply templates.

If no administrator is configured and the user table is empty, the CMS logs an error
at startup saying so. Fill the three values in and restart.

**Log in with either the username or the email address** — both work, and the login
field says so. The password is the one you configured.

Two things about a brand-new install that are easy to mistake for breakage:

- **The public site opens in maintenance mode.** `Advanced.MaintenanceModeEnabled`
  is seeded to `true`, so the site answers `503` with a maintenance page until you
  turn it off in **Site Settings → System**. That is intentional: a site with no
  content should not be publicly live. The change takes effect within a second or
  two (30 seconds at most, if the site cannot be reached at that moment) without a
  restart.
- **`/sitemap.xml` is empty until you publish something.** It is generated once at
  startup and then every two hours, so it exists immediately — it just has nothing
  to list yet.

---

## Languages: active vs published

A language carries two independent flags, and confusing them is the easiest way to
either leak an unfinished translation or wonder why a finished one is invisible:

| Flag | Column in the CMS | What it controls |
| --- | --- | --- |
| **Active** | "In the CMS" | The language can be authored in. Pages can be written and translated into it. Says nothing about the public site. |
| **Published** | "On the site" | The language is live. Its URL prefix routes, it appears in the language switcher, and its pages are searchable. |

Both must be on for visitors to see anything. That is what lets a translator work in
a new language for as long as they need with nothing leaking out, and what makes
"pull this language off the site" a single switch that does not also lock the
editors out of their own drafts.

The two seeded languages arrive with both flags on, so a fresh install can serve a
page the moment you publish one. **A language you add yourself starts unpublished** —
that is the point of the flag, and it is the box to tick when the translation is
ready to go live.

A new language is created **active but not published**. Publishing takes effect on
the public site within a second or two — no restart.

The same goes for every other change saved in the CMS. The public site keeps rendered
pages for up to an hour, but the CMS tells it to drop them after each successful save,
so edits show up on the next page view. The **Cache** screen's "Clear cache" button is
for changes made outside the CMS (a database restore, a manual edit), and
[docs/ARCHITECTURE.md](docs/ARCHITECTURE.md#public-site-caching) has the details.

In **Pages**, a language code that is authorable but not published is marked with an
eye-with-a-slash icon, so a page reading "Published" while being unreachable is
visible on the screen where you would notice it.

## Templates: linked vs copied

A template (Şablonlar) is a piece of GrapesJS content — a header, a footer, a
WhatsApp button — kept once and placed on many pages. How it is placed decides
what editing it later means:

| | **Linked** (`IsLinked`) | **Copy** |
| --- | --- | --- |
| On the page | A marker `<div class="elevare-tpl-ref" data-elevare-template-id="…">` | The content itself, wrapped in `elevare-tpl-snapshot` and stamped with the template's version at the time |
| On the public site | Resolved to the template's **current** content on every request (`TemplateResolutionService`) — edit the template, every page follows, no re-save | Whatever was copied; the template can change without it |
| In the page editor | **Read-only.** Clicking anywhere on it selects the whole template; its toolbar offers *edit the template* (opens the template editor in a new tab) and *cut the link*, which turns that instance into a copy. Moving, cloning and deleting it as a whole still work | Ordinary content, editable in place. When the source template's own content has changed since, a notice above the editor says when the copy was taken and when the template changed, with *open the template* and *don't show again* (for that version) — it never pulls the change in |

The two nest. A copy template can hold linked ones — an article layout with the
site's linked header and footer: a page made from it gets its own copy of the
layout while the header and footer stay linked and follow the site. A linked
template can hold another linked one (a menu with a linked search box); both the
public site and the editor resolve those level by level, up to five deep, and a
template that ends up inside itself is not nested again.

Only a change to what a copy actually took counts as "the template changed"
(`PageTemplate.ContentChangedAt`): re-saving a layout because the header inside it
changed does not flag every page made from it, since those pages get the header
live anyway. Copies that sit inside a linked template belong to that template, so
they are reported in its editor, not on every page that uses it.

Pick linked for anything that must be identical everywhere (site chrome). Pick a
copy when a page needs its own variation. Cutting the link is one-way; to get back
on the template, delete the copy and insert the template again.

Why linked content is locked on the page: its insides are replaced by the current
template on every request and on every editor open, so an edit made there was
saved, never shown, and gone the next time the page was opened — with nothing on
screen to say so. The lock is what makes that impossible.

### Hidden panels in the editor

Some blocks only show part of themselves until a visitor acts — the Mega Menu's
dropdown, the Search Box's results, the Pop-up, the Side Panel. In the canvas they
start closed, as on the live site. A ▼/▲ button on the block's floating toolbar
opens and closes one; the ⇕ button in the top bar opens or closes all of them at
once; selecting anything inside a closed panel from the Layers panel opens it. None
of that state is saved. The FAQ's answers and the Accordion's sections are the
opposite case: held open in the canvas so they can be edited in place, shipped
closed — tick "Başlangıçta açık" on a section to ship it open.

## Page listings

The "Sayfa Listesi" block lists pages: a page's children (the usual case — a
"Makaleler" page listing its articles), the page's siblings (its fellow articles),
a chosen page's children, or every published page in the page's own language
(system pages and the listing page itself left out) — optionally only those that
share a tag with the page. It holds one card, the template every result is cloned from; its fields
are filled from each page — title, description (else the article's opening
paragraph), share image (else the first image in the content, else the block's and
then the site's default image), publish date and tags — and a field the page has
nothing for is dropped rather than left showing the sample.

**Only live pages are listed.** In the editor the canvas shows the cards the block
will really render and, dimmed, the pages it leaves out (draft, archived, awaiting
approval, inactive); the trait panel says how many will be listed and links to the
ones that will not. None of that preview is saved with the page.

**Dates and order.** Cards show, and "newest/oldest" sort by, the page's publish
date (`PageInfo.PublishedAt`): the date an Article block on the page states,
otherwise the moment the page first went live. It is kept on every save and status
change, and backfilled for existing pages on start.

**URLs.** `?page=2`, `?tag=csharp` and `?q=…` drive the first listing on a page;
further listings use the same names suffixed with the block's id (`?page-ilist2=2`),
so each pages on its own. Nothing else in the URL is carried into links, the search
form or the canonical address, or keyed in the page cache. Page 1 is the page's own
address, a page past the last returns 404, search results are `noindex, follow`,
and the listed pages are added as an `ItemList` in structured data (a Category page's
draft graph uses `CollectionPage`). Search matches title and description, either
case.

## Blog & reading blocks

| Block | What it does |
| --- | --- |
| Kod Bloğu | Code with its language, an optional file name, line numbers and a "Kopyala" button. Edited in a plain text box ("Kodu düzenle" or a double-click), coloured on the server (`CodeHighlighter`: C#, JavaScript, TypeScript, JSON, HTML, XML, CSS, SQL, Bash, PowerShell, Python) — no highlighting script is shipped, and the colours are written only on pages that have code. |
| Önceki / Sonraki Yazı | Links to the pages published just before and after this one under the same parent, by publish date (`AdjacentPageResolutionService`). |
| İlgili Yazılar | A Sayfa Listesi preset: this page's siblings that share a tag with it, three of them. |
| Altyazılı Görsel | `<figure>` with a caption; opens full size on click. The Gallery block has the same "Tıklayınca büyüt" switch, stepping through its pictures with the arrow keys. |
| Okuma İlerleme Çubuğu | A thin bar showing how far through the article (or page) the reader is. |
| Sekmeler | Tabs with the ARIA tabs pattern (arrow keys, Home/End); without the script every panel simply shows. |
| Yukarı Çık Butonu | Appears after a screen's worth of scrolling. |

The Article block can show a reading time ("6 dk okuma") after its date; the public
site counts the words actually published (`ContentEnhancer`).

Each language has an article feed — `/feed.xml` for the default language,
`/{code}/feed.xml` for the others — with the pages whose type is Article, newest
first by publish date, linked from every page's head for feed readers and Google
Discover's "Follow".

## Google & sharing blocks

The "Google & Paylaşım" block group holds link-out buttons whose addresses are
built from a few fields — never typed as URLs — and that load nothing from a third
party (no script, no consent prompt, no CSP entry):

| Block | Goes to |
| --- | --- |
| Google Tercihli Kaynak | Google's "preferred source" choice for this site (`google.com/preferences/source?q=<host>`). The host is the site's own address from Site Settings unless another is typed; Google accepts a domain or subdomain only, never a path. The site has to be known to Google's source preferences tool for the choice to be offered. |
| Google Haberler'de Takip Et | The publication's Google News page (its address comes from Publisher Center). |
| Google'da Yorum Yaz | The "write a review" box of a Google Business Profile, from its Place ID. |
| Yol Tarifi Al | Google Maps directions to the address in Site Settings (or any typed one). |
| Takvime Ekle | Google Calendar, and an `.ics` file for Apple Calendar / Outlook, for an event. |
| YouTube Abone Ol | The channel from Site Settings with YouTube's own "Subscribe?" prompt. |
| Paylaşım Butonları | X, LinkedIn, Facebook, WhatsApp, Telegram, e-mail, copy link, and the device's share sheet — for the page's canonical address, filled in on the page by a small script. |

## Share tags: what you write is what ships

The Social Sharing panel (Page Settings › Sosyal Paylaşım) holds every Open Graph and
X field of a page, and the public site writes exactly those — an empty field writes
no tag; nothing is filled in behind the author's back. "Otomatik doldur" fills the
empty fields, once, from what is already known: the page title and description (or
Site Settings' default description), the first image in the content (or Site
Settings' default share image) with its size, type and alt text from the media
library, the page's address, language, publish and update dates and tags, the site
name and the X profile. The SEO analysis checks the share tags as the fields say
them — missing title, description or image, an image without a size, too small or
without a description.

## Site codes, and switching one off on a page

Site Codes (Site Kodları) are the snippets written into every public page — the
theme's base CSS and brand variables, a consent banner, analytics, a chat widget —
each named, ordered, placed (head start/end, body start/end) and switchable
site-wide. A page can opt out of individual ones: the **Site Kodları** tab in the
page editor's Page Settings lists them by placement and name, and a ticked row means "not on
this page". The list shows names only; the content stays behind the
`CustomCode.Author` permission on the Site Codes screen.

It is exclusion only, by design. A site code is site-wide by definition and the
page is the exception; a page that needs code of its own has the builder's
custom-code block, which is what a "per-page site code" would be anyway. The
exclusions live in `PageInfoSiteCodeExclusions` — a join table, cascade on both
sides, so purging a snippet from the Trash takes its exclusions with it — and
apply on the public site (404/500 pages included) and in the editor's canvas.

## The Trash

Deleting a page, template, media file, language, site setting, site code, task, note,
reminder or bulk content edit is a soft delete: the row stays and **Trash** can put it
back. Four rules keep that from turning into an archive nobody can use.

**One tombstone per address.** Deleting a page purges any older deleted row on the
same slug and language. The Trash answers "undo the last delete", not "show me every
attempt" — without this, a delete/recreate loop leaves a pile of rows that can never
be restored, because a live page has taken the address back.

**Recreating asks first.** If you create a translation in a language that already has
a deleted version, the CMS shows when it was deleted and by whom, and offers two
choices: restore it with its content and history, or start over. Starting over purges
the old row, since once the new page holds the address that row could never have been
restored anyway.

**A restored site code comes back switched off.** Everything else in the Trash comes
back as something you then go and look at. A site code comes back as a script that
runs in every visitor's browser on the next page load, so restoring the row and
re-serving it are kept as two decisions: the row returns, `IsEnabled` does not, and
you turn it on from Site Codes once you have read what you are about to re-publish.

**Permanent deletion is real, and refuses when it shouldn't be.** Each row has a
*Delete permanently* action, and the toolbar has *Empty the Trash*. Both skip pages
that would take something else with them:

| Blocked when | Why |
| --- | --- |
| The page holds form submissions | Those are visitor-entered enquiries, not content. |
| The page still has sub-pages | Their parent reference is not nullable. |

The button is disabled with the reason in its tooltip, and "Empty the Trash" reports
how many rows it purged *and* how many it protected — an empty-trash that leaves rows
on screen otherwise reads as a broken button.

**Automatic clean-up.** `Retention:TrashDays` (default 30) lets the nightly retention
job destroy trashed pages past their window. Set it to `0` to keep the Trash forever.
The same safety checks apply, so the timer can never quietly delete visitor data.

## Redirects

Renaming, deleting or archiving a published page writes a redirect rule
automatically, so an old URL keeps working instead of turning every existing link
and search result into a 404. **Content → Redirects** lists them, and adds the rules
no page edit could produce: URLs that predate this CMS, campaign short links, or a
retired page that should answer `410 Gone` rather than point anywhere.

The screen leads with a diagnosis, because a broken redirect is silent — nothing in
the CMS looks wrong, and only the visitor sees the 404:

| State | What it means |
| --- | --- |
| **Dead target** | The destination is not a published page. The rule sends people to a 404. |
| **Loop** | The chain returns to where it started. Visitors never arrive anywhere. |
| **Chained** | More than one hop. It works, but each hop costs a round trip and crawlers stop following. |
| **410 Gone** | Deliberately retired with no successor. |
| **External** | Points at another site, which this CMS cannot check. |

A rule created by a page rename stays *bound* to that page: its target follows the
page's current address, so renaming twice never leaves a stale chain behind. Editing
such a rule by hand detaches it — the screen says so before you save.

Saving is refused for a rule that would sit on a live page's own URL (it would hide
that page) or close a redirect loop.

## Security

What the apps do for themselves, so a deployment behind plain Kestrel is not
meaningfully weaker than one behind a hardened proxy.

**Response headers** are set by both apps on every response, error pages included:
`X-Content-Type-Options: nosniff`, `Referrer-Policy: strict-origin-when-cross-origin`,
a `Permissions-Policy` that denies camera, microphone, payment and USB, and
anti-clickjacking via both `X-Frame-Options` and CSP `frame-ancestors` — `none` for
the CMS, which nothing should ever embed, and `self` for the public site.

There is deliberately **no `script-src` policy**. Site Codes exists so an operator
can inject Google Analytics, Tag Manager, a Meta Pixel or a consent banner into
their pages; a script policy strict enough to be worth having would break that on
the first snippet pasted. Doing it properly means letting the operator declare their
own allowed sources, which is a feature rather than a header, and is not built yet.

**Sign-in is protected twice over**, because the two attacks are different shapes:

| Layer | Against |
| --- | --- |
| Account lockout — 5 failures, then 15 minutes (Identity) | Guessing one account's password, however the attempts arrive |
| Rate limit — 20 attempts per IP per 5 minutes (`RateLimiting:LoginPermitLimit`) | Spraying one password across many accounts, which no per-account counter sees |

A locked account is told so, rather than being handed another "wrong password" and
left retrying against a wall. Behind a reverse proxy, set
`RateLimiting:TrustForwardedForHeader` to `true` — and only then, because without a
proxy that header is attacker-controlled and trusting it disables the limiter while
appearing to leave it on.

Behind any reverse proxy (nginx, IIS's own ARR, a load balancer), also set
`ASPNETCORE_FORWARDEDHEADERS_ENABLED=true`. Without it, the app never sees the
original scheme/host from `X-Forwarded-*` and treats every request as plain HTTP —
secure cookies, `IsHttps` checks and anything that redirects to HTTPS all silently
misbehave. The Docker Compose file sets this already; add it yourself for Option B
or C.

The public site has its own per-endpoint limits for form submissions, search,
telemetry and client error reports; page rendering is deliberately unlimited so a
traffic spike never becomes 429s for real visitors.

## Configuration

Every setting is read from configuration — nothing is hardcoded. ASP.NET Core reads,
in increasing order of precedence: `appsettings.json` → `appsettings.{Environment}.json`
→ user secrets (Development) → environment variables.

Environment variables use `__` where the JSON path uses `:` — `Jwt:SecretKey`
becomes `Jwt__SecretKey`. That is what `docker-compose.yml` sets.

### Required

| Key | Applies to | Notes |
| --- | --- | --- |
| `ConnectionStrings:DefaultConnection` | both | Same database for both apps. |
| `Jwt:SecretKey` | CMS | 32+ characters; startup fails below that. |
| `Jwt:Issuer`, `Jwt:Audience` | CMS | |
| `Preview:SigningKey` | both | Must match byte for byte. |
| `Cache:ClearSecret` | both | Must match byte for byte. Ships empty — set your own. |
| `Seed:SuperAdmin:UserName` / `:Email` / `:Password` | CMS | First run only; all three or none. |

### Optional

| Key | Applies to | What it does |
| --- | --- | --- |
| `Seed:Editor:*` | CMS | Same shape as `Seed:SuperAdmin`, seeds a second account. |
| `DataProtection:KeyPath` | CMS | Directory for the key ring. Unset uses the framework default, which is fine outside containers. |
| `Email:*` | both | SMTP host, port, credentials, sender. Without it the CMS cannot send form replies. Can also be set from the CMS's **Site Settings → Sırlar** screen instead (`SecretsManage` permission) — that value wins over this one when set; see [Integration secrets](#integration-secrets). |
| `FileStorage:*` | both | Upload folder, 50 MB size cap, allowed MIME types. |
| `ObjectStorage:*` | CMS | `Local` (default) or an S3-compatible bucket. Same "Sırlar screen overrides this" story as `Email:*` above. |
| `Backup:*` | CMS | Directory, how many to keep, timeout. Empty directory falls back to a folder next to the running app. |
| `Retention:*` | CMS | How long analytics and logs are kept (90 days each by default), and how long the Trash holds deleted items (`TrashDays`, 30; `0` keeps it forever). |
| `Redis:ConnectionString` | both | Redis cache provider on the site; on the CMS it also makes admin sessions survive a restart. **Point the CMS at a different Redis database than the site** (e.g. append `,defaultDatabase=1`) — clearing the site cache runs a whole-database `FLUSHDB`, and if the two share a database that wipes every admin's session along with it. The Docker Compose file already sets this up. |
| `Captcha:SecretKey` | web | Paired with the provider chosen in Site Settings. Same "Sırlar screen overrides this" story as `Email:*` above — decrypting it needs the CMS's Data Protection key ring, which is why `docker-compose.yml` mounts `cms-keys` read-only into the `web` service too. |
| `RateLimiting:*` | web | Per-endpoint limits for the public endpoints. |
| `Serilog:*` | both | Log levels and sinks. The console sink is always on; `docker compose logs` reads it. |
| `Serilog:SeqUrl`, `Serilog:SeqApiKey` | both | Optional. Empty (the default) means the console is the only sink. Given a Seq address, a second sink is registered at startup and both apps ship their structured logs there. It is registered **only** when the URL is set, because a Seq address that resolves to nothing fails silently — the sink buffers, drops, and the logs look configured. `docker compose -f docker-compose.yml -f docker-compose.dev-tools.yml up -d` starts a local Seq and points both apps at it; its UI is at <http://localhost:5341>. |

Settings that belong to the *site* rather than the *deployment* — site name, logo,
SEO defaults, analytics snippets, maintenance mode, cache provider, CDN base URL — live
in the database and are edited in the CMS under **Site Settings**, **Site Codes** and
**SEO**, not in `appsettings.json`.

#### Integration secrets

A third category sits between the two: real credentials for systems outside the CMS
(SMTP password, CAPTCHA secret key, S3/CDN access keys). These can be set either way —
`appsettings.json`/environment variables as shown above, or the CMS's
**Site Settings → Sırlar** screen (gated on the `SecretsManage` permission, granted to
SuperAdmin by default). Whichever has a value wins, and the database wins when both do.
Unlike plain Site Settings, these are never stored as plain text — they're encrypted
with the same ASP.NET Data Protection key ring the CMS already uses for sessions, and a
saved value is never redisplayed, only replaced. Unlike the file/env fallback, a value
entered on that screen applies live — no restart, including switching
`ObjectStorage:Provider` itself between local disk and S3. Saving a CAPTCHA secret also
notifies the public site over the internal network (the same `Cache:ClearSecret` channel
cache-clearing already uses) so it picks it up within a second or two as well; the CMS
tells you if that notification fails (e.g. the site was unreachable) so you know to save
again once it's back.

---

## Build and test

```bash
dotnet build Elevare.sln
```

```bash
dotnet test Elevare.sln
```

The tests cover the query and command handlers, the HTML sanitiser, the preview
token signer, and contract tests that assert the CMS's schema and the site's read
models have not drifted apart.

The build runs with `TreatWarningsAsErrors`; the analyzer rules the codebase opts out
of are in the root `.editorconfig`.

### Migrations

The CMS applies pending migrations on startup, so day to day you do not run anything.
To add one:

```bash
dotnet ef migrations add <Name> --project src/cms/Infrastructure/Persistence --startup-project src/cms/Presentation/Wasm/Wasm
```

The generated files can be committed as they are: the migrations folder has its own
`.editorconfig` that relaxes the style rules for EF's output.

---

## Notes

- **Admin sessions survive a restart when Redis is configured.** The JWT lives in
  the session, so where the session is kept decides what a restart does. Set
  `Redis:ConnectionString` — the Docker stack does — and sessions outlive the
  container; leave it unset and they are held in process, which is fine for a single
  instance you rarely restart. Either way `DataProtection:KeyPath` keeps the cookie
  decryptable across a recreate, so a lost session is a clean redirect to the login
  page rather than cryptographic errors on already-rendered pages. Use a database
  index dedicated to the CMS (the Docker stack's `,defaultDatabase=1`) — the site's
  own cache clear is a whole-database `FLUSHDB`, and a shared database means every
  admin gets signed out the moment someone clears the cache.
- **The site tolerates the CMS being ahead of it.** The site retries transient SQL
  failures, so on a first run it will wait out the schema being created rather than
  serving a wall of 500s.
- **`/health`** on both apps answers `200 Healthy` without authentication, and stays
  `200` while the site is in maintenance mode — maintenance is a statement about the
  site, not about the process.

---

## Contributing

Bug reports, ideas and pull requests are welcome — see
[CONTRIBUTING.md](CONTRIBUTING.md) for the conventions that are specific to this
codebase, and [SECURITY.md](SECURITY.md) if you have found a vulnerability (please
do not open a public issue for those).

## License

[MIT](LICENSE) © Oğuzhan Karagüzel
