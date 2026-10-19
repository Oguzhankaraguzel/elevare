# Changelog

Notable changes to Elevare. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and versions follow
[Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [1.2.0] - 2026-10-14

### Changed

- **A save clears only the pages it touched.** The CMS reads what each save actually
  wrote and tells the site which cached pages are stale: a page's content clears that
  page and the lists showing it, a linked template every page using it, a note, task
  or user nothing at all. Site-wide data and anything not understood still clear the
  whole site. Before, every save — and the sitemap rebuild every two hours — emptied
  the site's cache.
- **Rendered pages are kept for a day** instead of an hour, since a change now reaches
  them on save.
- **The site warms its own cache**: after an eviction and after start-up it renders
  the pages that are not cached, so a visitor rarely waits for a render.
- **The web image is compiled ahead of time** (ReadyToRun), so the first requests
  after a deploy no longer wait for the JIT.
- **Card and grid images get a fitting `sizes` hint** from the page's own CSS, and
  lazy images let the browser measure them (`sizes="auto"`): a card a third of the
  page wide no longer downloads the full-width file.
- **Slider images after the first wait for the page to load** instead of downloading
  with it.
- **A missing page is answered without an exception**: one log line instead of a
  stack trace, and the 404 is cached for five minutes.

### Fixed

- **Deploys no longer fill the server's disk.** Each build left its intermediate
  layers behind and nothing removed them; the deploy now prunes build cache older
  than a day, and container logs are capped at 3 × 10 MB per service.

### Security

- Microsoft ASP.NET Core and EF Core packages updated to 9.0.21 (CVE-2026-96230 in
  `Microsoft.AspNetCore.Components.WebAssembly.Server`).

## [1.1.0] - 2026-10-14

### Added

- **Getting in without mail.** An administrator can make a one-time password link
  (valid for 48 hours) and pass it on themselves; users can ask for a reset link
  from the sign-in screen; a user given a temporary password is made to change it
  at first sign-in. A SuperAdmin locked out can be recovered with
  `ADMIN_RESET_PASSWORD=true`.
- **Redirects can be 301 or 302**, chosen per rule and in CSV imports.
- **The CMS shows the site it manages** in the sidebar, tab titles and sign-in
  screen, with Elevare's version beside it.
- **Languages and homepages can be taken off the site safely**: unpublishing asks
  first and says what readers will see instead.

### Changed

- **Secrets are no longer shown.** The Secrets screen keeps every input empty and
  says instead whether a value is saved, comes from the server's configuration
  (`.env`) or is missing; saving or clearing one asks for confirmation. Mail set
  up only in `.env` no longer looks unconfigured.
- Seeded placeholder secrets are released so the server's own values take effect.
- Section sitemaps are served from the site root.
- The Turkish documentation was rewritten in natural Turkish.

### Fixed

- Buttons keep their content when the forms plugin loads.
- Name correction in the user and profile screens; empty sidebar sections are hidden.

## [1.0.0] - 2026-10-04

The first public release. Elevare is a .NET 9 content management system — a Blazor
Server admin panel — and the ASP.NET Core MVC site it publishes to, sharing one
PostgreSQL database. It has run a live bilingual site in production before this
release; this is that code, opened up.

### Included

- **Visual page building** with GrapesJS: drag-and-drop blocks, linked and copied
  templates (a linked one updates every page that uses it), hidden-panel blocks such
  as mega menus and pop-ups, and a signed preview link to share before publishing.
- **Approval workflows** with configurable steps and reviewer roles, and a diff of
  the reader-visible text rather than the HTML the builder rewrites on every save.
- **Role-based permissions**, editable at runtime and enforced server-side in the
  request pipeline.
- **Multi-language content**: prefix routing, a separate "authorable" and
  "published" switch per language, a translation matrix on the Pages screen, and a
  default-language switch that moves every address, rewrites the links to them and
  leaves 301s behind.
- **SEO**: per-page meta, Open Graph and X cards, a structured-data editor with a
  draft builder, editable `robots.txt` and `llms.txt`, a sectioned multilingual
  sitemap, RSS feeds per language, and redirect management that reports dead
  targets, loops and chains.
- **Images**: one upload becomes a master plus WebP and resized copies, served
  through `<picture>`/`srcset` with a `sizes` hint taken from the page's own CSS.
- **Blocks** for articles and blogs (code with server-side colouring, reading time,
  table of contents, previous/next, related posts, page listings with tags, search
  and paging), forms with consent, spam protection and attachments, and Google and
  sharing buttons that load nothing from a third party.
- **Editing safety**: an editor opened before someone else saved is told so instead
  of silently overwriting their work; unsaved changes are guarded.
- **Operations**: scheduled backups, data retention, a job dashboard, cache control
  that keeps the site's page cache in step with every save, an audited Trash,
  integration secrets encrypted in the database, and an opt-in deploy-on-push step.
- **Setup**: Docker Compose, `dotnet run`, or a folder publish to IIS; the first run
  creates the database from a single migration and seeds roles, languages, settings
  and ready-made home, 404, 500 and maintenance pages.
