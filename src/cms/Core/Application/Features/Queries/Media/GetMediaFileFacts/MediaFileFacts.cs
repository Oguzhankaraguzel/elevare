namespace Application.Features.Queries.Media.GetMediaFileFacts;

public sealed record MediaFileFacts(int? Width, int? Height, string MimeType, string? AltText);
