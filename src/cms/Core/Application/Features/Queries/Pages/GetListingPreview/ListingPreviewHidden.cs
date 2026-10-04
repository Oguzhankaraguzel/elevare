namespace Application.Features.Queries.Pages.GetListingPreview;

/// <summary>A page the listing leaves out, and why.</summary>
/// <param name="Reason">"draft", "archived", "pending" or "inactive".</param>
public sealed record ListingPreviewHidden(int Id, string Title, string Reason);
