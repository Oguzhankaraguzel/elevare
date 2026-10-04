namespace Wasm.Models;

/// <summary>
/// A "Sayfa Listesi" block's settings, as the editor sends them when it asks what
/// the block will list — see getListingPreview in grapes-editor.js.
/// </summary>
public sealed record ListingPreviewRequest(string? Source, int? SourcePageId, string? Sort, string? TagSlug, int Take, bool RelatedTags = false);
