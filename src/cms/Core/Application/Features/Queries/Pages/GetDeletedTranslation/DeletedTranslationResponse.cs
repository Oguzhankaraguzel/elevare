namespace Application.Features.Queries.Pages.GetDeletedTranslation;

/// <param name="DeletedByName">Who deleted it — the answer to "was this me, or a colleague?"</param>
public sealed record DeletedTranslationResponse(
    int Id,
    string? Title,
    string Slug,
    DateTime? DeletedAtUtc,
    string? DeletedByName);
