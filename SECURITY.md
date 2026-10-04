# Security Policy

**English** · [Türkçe](SECURITY.tr.md)

## Reporting a vulnerability

Please report privately, not in a public issue — an open issue is a disclosure, and
it reaches attackers before it reaches a fix.

Use GitHub's [private vulnerability reporting](https://github.com/Oguzhankaraguzel/elevare/security/advisories/new)
on this repository. If that is not available to you, email the address on the
maintainer's GitHub profile with `SECURITY` in the subject.

Useful to include, as far as you have it: what an attacker can do, the steps to
reproduce, which of the two applications is affected, and the version or commit you
tested. A proof of concept helps; a working exploit is not required.

This is a personal project, not a funded one — expect a first reply within a few
days rather than within hours, and no bug bounty. Fixes for confirmed issues are
prioritised over everything else.

Please do not run automated scanners against anyone else's deployment of Elevare.

## Supported versions

Only the latest release on `master` is supported. There are no backported security
fixes for older tags.

## What Elevare does for itself

For operators evaluating a deployment, the protections in the box:

- **Response headers** on both apps: `nosniff`, `Referrer-Policy`, a restrictive
  `Permissions-Policy`, and anti-clickjacking via `X-Frame-Options` plus CSP
  `frame-ancestors`.
- **Sign-in** is protected by Identity account lockout (5 failures, 15 minutes) and
  an IP rate limit (20 attempts per 5 minutes), which cover single-account guessing
  and password spraying respectively.
- **Rate limits** on the public site's write and query endpoints — form submissions,
  search, telemetry, client error reports.
- **Authorisation** is enforced server-side in the MediatR pipeline
  (`IRequirePermission`), not only in the UI, so hiding a button is never the only
  thing standing between a user and an action.
- **Raw HTML and script** in page content can only be saved by roles holding
  `CustomCode.Author`; everything else is sanitised.
- **Passwords** are hashed by ASP.NET Core Identity. Secrets come from configuration
  and nothing ships with a working default — the stack refuses to start with them
  empty.

## Known gaps

Stated plainly, because a security policy that only lists strengths is not useful:

- **No `script-src` Content-Security-Policy.** Site Codes exists so an operator can
  inject analytics and consent scripts into their own pages; a script policy strict
  enough to matter would break that feature. Locking it down properly means letting
  the operator declare their allowed sources, and that is not built yet.
- **The Hangfire dashboard** is gated on the `Hangfire.Access` permission, checked
  against the live role-permission cache rather than the sign-in snapshot, so a
  revoked permission takes effect immediately. It has no credential of its own.
- **Uploaded files** must have an extension that agrees with the declared content
  type, checked against the same map the serving endpoint uses. Without that
  agreement a file called `evil.html` could be uploaded as `image/png` and served
  back as `text/html` from the CMS's own origin. Files are **not** scanned for
  malware, and SVG is worth thinking about before enabling: it is XML that can carry
  script, and it renders as itself when opened directly.
- **Multi-tenancy is not a goal.** Everyone with CMS access can reach every site
  setting their role permits; there is no isolation boundary between content owners.

## Deployment notes that affect your security, not ours

- Put the apps behind TLS. HSTS is enabled outside development, which assumes it.
- Behind a reverse proxy, set `RateLimiting:TrustForwardedForHeader` to `true` —
  and **only** behind one. Without a proxy that overwrites it, `X-Forwarded-For` is
  attacker-controlled, and trusting it disables the rate limiter while leaving it
  looking enabled.
- Change `Cache:ClearSecret`, `Jwt:SecretKey` and `Preview:SigningKey` from the
  values you generated for testing before going live, and keep them out of version
  control.
- The database user does not need superuser privileges. The official `postgres`
  image in `docker-compose.yml` creates whatever `POSTGRES_USER` names (`elevare`
  by default) as a superuser automatically — convenient for local development, but
  a real deployment should create a narrower role instead and point
  `ConnectionStrings:DefaultConnection` at that.
