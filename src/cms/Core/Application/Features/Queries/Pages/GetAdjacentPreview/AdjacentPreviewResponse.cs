namespace Application.Features.Queries.Pages.GetAdjacentPreview;

/// <summary>Null on a side the public site leaves empty.</summary>
public sealed record AdjacentPreviewResponse(AdjacentPreviewItem? Previous, AdjacentPreviewItem? Next);
