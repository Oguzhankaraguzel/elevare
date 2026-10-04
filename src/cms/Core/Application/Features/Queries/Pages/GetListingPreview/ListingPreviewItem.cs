namespace Application.Features.Queries.Pages.GetListingPreview;

/// <summary>One card, filled the way the public site fills it.</summary>
public sealed record ListingPreviewItem(
    int Id, string Title, string Path, string? Summary, string? Image, string DateText, string? Tags);
