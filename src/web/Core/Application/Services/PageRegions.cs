namespace Application.Services;

/// <summary>The three regions a rendered page is split into.</summary>
/// <param name="Header">Markup for the layout's banner region; empty when the page has none.</param>
/// <param name="Main">Everything else — the page's own content.</param>
/// <param name="Footer">Markup for the layout's contentinfo region; empty when the page has none.</param>
public sealed record PageRegions(string Header, string Main, string Footer);
