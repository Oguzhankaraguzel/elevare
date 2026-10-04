namespace Application.Features.Queries.Search.SearchPublicContent;

public sealed record SearchResultItemResponse(string Title, string Path, string Excerpt, string? Category);
