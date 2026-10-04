using Application.Abstraction.Data;
using Application.Security;
using Domain.Entities.SiteCodeSnippets;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Abstraction.Messaging;
using SharedKernel.Concrete;

namespace Application.Features.Commands.SiteCodeSnippets.SaveSiteCodeSnippet;

internal sealed class SaveSiteCodeSnippetCommandHandler(ICmsApplicationDbContext db)
    : ICommandHandler<SaveSiteCodeSnippetCommand, SaveSiteCodeSnippetResult>
{
    public async Task<Result<SaveSiteCodeSnippetResult>> Handle(
        SaveSiteCodeSnippetCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return Result.Failure<SaveSiteCodeSnippetResult>(SiteCodeSnippetErrors.NameRequired);

        // A preset rewrites the author's input into known-good markup and decides
        // where it goes; Custom takes the paste as-is at the chosen placement.
        IReadOnlyList<SiteCodePart> parts;
        if (request.Preset == SiteCodePreset.Custom)
        {
            parts = [new SiteCodePart(request.Kind, request.Placement, request.RawInput ?? string.Empty, Suffix: null)];
        }
        else
        {
            Result<IReadOnlyList<SiteCodePart>> built = SiteCodePresetFactory.Build(request.Preset, request.RawInput);
            if (built.IsFailure)
                return Result.Failure<SaveSiteCodeSnippetResult>(built.Error);

            parts = built.Value;
        }

        // Validate every part before writing any of them: a preset that yields two
        // rows must not leave half of itself behind when the second one is refused.
        List<string> warnings = [];
        foreach (SiteCodePart part in parts)
        {
            SiteCodeValidationResult validation = SiteCodeValidator.Validate(part.Kind, part.Content);
            if (!validation.IsValid)
                return Result.Failure<SaveSiteCodeSnippetResult>(validation.Error!);

            warnings.AddRange(validation.Warnings);
        }

        if (request.Id is int id)
        {
            SiteCodeSnippet? existing = await db.SiteCodeSnippets
                .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

            if (existing is null)
                return Result.Failure<SaveSiteCodeSnippetResult>(SiteCodeSnippetErrors.NotFound);

            // Editing only ever touches the row in hand. A preset that would now
            // produce two parts (someone switching Custom → Tag Manager on an
            // existing row) writes the first here and the rest as new rows below.
            SiteCodePart first = parts[0];
            existing.Name = request.Name.Trim();
            existing.Preset = request.Preset;
            existing.Kind = first.Kind;
            existing.Placement = first.Placement;
            existing.Content = first.Content;
            existing.IsEnabled = request.IsEnabled;
            existing.Notes = request.Notes;

            foreach (SiteCodePart extra in parts.Skip(1))
                db.SiteCodeSnippets.Add(await BuildNewAsync(request, extra, cancellationToken));

            return Result.Success(new SaveSiteCodeSnippetResult(parts.Count - 1, warnings.Distinct().ToList()));
        }

        foreach (SiteCodePart part in parts)
            db.SiteCodeSnippets.Add(await BuildNewAsync(request, part, cancellationToken));

        return Result.Success(new SaveSiteCodeSnippetResult(parts.Count, warnings.Distinct().ToList()));
    }

    private async Task<SiteCodeSnippet> BuildNewAsync(
        SaveSiteCodeSnippetCommand request, SiteCodePart part, CancellationToken cancellationToken)
    {
        return new SiteCodeSnippet
        {
            Name = part.Suffix is null ? request.Name.Trim() : $"{request.Name.Trim()} ({part.Suffix})",
            Preset = request.Preset,
            Kind = part.Kind,
            Placement = part.Placement,
            Content = part.Content,
            IsEnabled = request.IsEnabled,
            Notes = request.Notes,
            SortOrder = await NextSortOrderAsync(request.Preset, part.Placement, cancellationToken),
        };
    }

    /// <summary>
    /// New rows go last within their placement, except consent, which is pinned
    /// first — a consent banner that loads after the trackers it is meant to gate
    /// is decoration, and nobody would notice from looking at the page.
    /// </summary>
    private async Task<int> NextSortOrderAsync(
        SiteCodePreset preset, SiteCodePlacement placement, CancellationToken cancellationToken)
    {
        if (preset == SiteCodePreset.CookieConsent)
            return 0;

        int highest = await db.SiteCodeSnippets
            .Where(s => s.Placement == placement)
            .Select(s => (int?)s.SortOrder)
            .MaxAsync(cancellationToken) ?? 0;

        return highest + 10;
    }
}
