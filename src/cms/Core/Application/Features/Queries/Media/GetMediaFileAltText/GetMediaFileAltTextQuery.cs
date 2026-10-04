using SharedKernel.Abstraction.Messaging;

namespace Application.Features.Queries.Media.GetMediaFileAltText;

/// <summary>
/// Looks up the alt text stored for a media file by its site-relative path — for
/// a block (the page builder's Navbar logo, so far) that has a URL on hand but not
/// the id behind it. Deliberately open to any signed-in user, same reasoning as
/// <see cref="Application.Features.Queries.Media.GetMediaFiles.GetMediaFilesQuery"/>.
/// </summary>
public sealed record GetMediaFileAltTextQuery(string FilePath) : IQuery<string?>;
