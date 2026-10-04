# Contributing

**English** · [Türkçe](CONTRIBUTING.tr.md)

Thanks for looking. This file covers the things that are specific to Elevare — the
conventions a build failure will teach you the hard way, and the ones no compiler
enforces at all.

For how the system is put together, read [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md).

## Getting it running

Either route in the [README](README.md) works. Docker is the shorter one and needs
nothing but Docker itself; `dotnet run` needs the .NET 9 SDK and a PostgreSQL server
you can reach.

Local settings live in `appsettings.Development.json` next to each app. Those files
are git-ignored — copy the `.example` beside them and fill in your own values. If a
change of yours needs a new setting, add it to the `.example` too, or the next person
to clone will not know it exists.

```bash
dotnet build Elevare.sln
```

```bash
dotnet test Elevare.sln
```

Both must pass before a pull request is opened. CI runs exactly these, plus a build
of both Docker images and a check for dependencies with known advisories.

## Things that will fail your build

**Warnings are errors.** `TreatWarningsAsErrors` is on, with Sonar, the .NET
analyzers and the IDE style rules all active. A rule the codebase deliberately opts
out of is turned off in the root `.editorconfig`, with a comment saying why — if you
find yourself wanting to suppress a rule inline, consider whether the code is telling
you something first.

**Build with the .NET 9 SDK.** The projects target `net9.0`. A newer preview SDK will
happily compile things the 9 compiler rejects — unused `async`, certain Razor
constructs — and the failure then only shows up in CI or in the Docker build. If you
have a preview SDK installed, the Docker build is the quickest way to check.

**EF migrations are committed as generated.** The migrations folder has its own
`.editorconfig` that relaxes the style rules for EF's output, so there is nothing to
convert:

```bash
dotnet ef migrations add YourMigrationName --project src/cms/Infrastructure/Persistence --startup-project src/cms/Presentation/Wasm/Wasm
```

If the build still complains about a generated `new Guid("00000000-…")` default
(S4581), replace it with `Guid.Empty`. A migration that needs something the model
cannot express — an extension, a functional index — goes in as
`migrationBuilder.Sql(...)`, with a comment saying why.

## Conventions no compiler checks

**Handlers return `Result`, they do not throw.** A failure is
`Result.Failure(SomethingErrors.Reason)`, where the error constant lives beside the
entity it describes. Do not build an `Error` inline at the call site: the code is the
translation key, and one invented in place will have no message.

**Every error code needs a message in both languages.** Add it to
`ErrorMessages.resx` *and* `ErrorMessages.tr.resx`. `ErrorMessageCoverageTests`
fails the build if you forget, because the fallback is the English description and
the only person who ever sees that is a Turkish user hitting your new failure path.

The same applies to UI strings, in `CmsMessages.resx` / `CmsMessages.tr.resx`.

**Code and comments are English; the UI is bilingual.** Commit messages either way.

**Comments explain why, not what.** The codebase is fairly heavily commented, but
almost every comment answers a question the code cannot: why this order, why this
looks wrong but is not, what broke last time. A comment restating the line above it
will be asked about in review.

## Tests

New behaviour comes with a test. The suites live in `tests/cms` and `tests/web`, and
run against the real `DbContext` on EF's in-memory provider, so a handler test
exercises the production mapping and query filters rather than a hand-rolled fake.

Two things the in-memory provider cannot do, both learned here the hard way:

- It ignores unique indexes and filtered indexes.
- It does not support `ExecuteUpdate` / `ExecuteDelete`. Code that must be testable
  should go through the change tracker instead.

Name tests as sentences — `A_deleted_translation_can_be_created_again` — and, where
the reason is not obvious, say in a comment what real failure the test is guarding
against.

## Pull requests

Small and focused beats large and comprehensive. Say what changed, why, and how you
verified it; "the build passes" is not verification, since CI already does that.

If you are planning something structural, open an issue first — it is cheaper to
disagree about an approach than about a finished branch.

## Security

Please do not open a public issue for a vulnerability. [SECURITY.md](SECURITY.md)
has the private route.
