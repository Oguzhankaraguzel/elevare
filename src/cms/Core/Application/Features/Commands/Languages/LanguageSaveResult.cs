namespace Application.Features.Commands.Languages;

/// <param name="DefaultChanged">The language became the default, so every page's address was recomputed.</param>
/// <param name="MovedAddresses">Pages whose address changed.</param>
/// <param name="UpdatedRecords">Pages, templates, settings and redirects whose stored links were pointed at the new addresses.</param>
public sealed record LanguageSaveResult(bool DefaultChanged, int MovedAddresses, int UpdatedRecords)
{
    public static readonly LanguageSaveResult Unchanged = new(false, 0, 0);
}
