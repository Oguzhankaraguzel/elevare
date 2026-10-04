namespace Application.Features.Commands.SiteCodeSnippets.SaveSiteCodeSnippet;

/// <summary>
/// What the save produced.
/// </summary>
/// <param name="CreatedCount">
/// More than one when a preset genuinely needs several rows — Tag Manager's head
/// script plus its body noscript. Surfacing the number is how the author learns a
/// second entry appeared without going looking for it.
/// </param>
/// <param name="Warnings">
/// Codes from <c>SiteCodeValidator</c> for things that are legal but worth knowing
/// (currently <c>DocumentWrite</c>). The save already happened.
/// </param>
public sealed record SaveSiteCodeSnippetResult(int CreatedCount, IReadOnlyList<string> Warnings);
