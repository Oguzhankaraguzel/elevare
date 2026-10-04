## What does this change?

<!-- One or two sentences. If it fixes an issue, write "Fixes #123". -->

## Why?

<!-- The problem being solved. A reviewer who knows why can spot a better how. -->

## How was it verified?

<!-- Which tests you added or ran, and what you checked by hand. "Builds fine" is
     not verification; the build already runs in CI. -->

## Checklist

- [ ] `dotnet build Elevare.sln` passes — the build treats warnings as errors, so a
      new analyzer complaint fails it.
- [ ] `dotnet test Elevare.sln` passes, and behaviour changes come with a test.
- [ ] User-visible strings are in **both** `CmsMessages.resx` and `CmsMessages.tr.resx`
      (error messages go in `ErrorMessages*.resx`).
- [ ] A new EF migration, if any, was generated with `dotnet ef migrations add` and
      anything the model cannot express is a commented `migrationBuilder.Sql(...)`.
- [ ] No secrets, connection strings or personal data in the diff.
