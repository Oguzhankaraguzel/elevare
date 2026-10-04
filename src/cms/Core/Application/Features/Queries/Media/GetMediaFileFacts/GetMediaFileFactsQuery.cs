using SharedKernel.Abstraction.Messaging;

namespace Application.Features.Queries.Media.GetMediaFileFacts;

/// <summary>
/// What the media library knows about an image, by the address a page uses for it —
/// "/uploads/…" or the same path under a full URL. The Social Sharing panel fills
/// an image's width, height, type and alt text from this, so nobody has to look
/// them up. Null when the address is not a library file. Open to any signed-in
/// user, same reasoning as <see cref="GetMediaFileAltText.GetMediaFileAltTextQuery"/>.
/// </summary>
public sealed record GetMediaFileFactsQuery(string Address) : IQuery<MediaFileFacts?>;
