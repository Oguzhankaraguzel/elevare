using Application.Features.Queries.Media.GetMediaFiles;
using SharedKernel.Extensions.Strings;

namespace Wasm.Models.Media;

/// <summary>
/// Turns the media library's file list into the picker's list.
/// </summary>
public static class MediaPickerProjection
{
    /// <summary>
    /// Collapses each variant group down to one entry, so a picture uploaded at
    /// three sizes appears once rather than three times — picking "the 640px one" is
    /// not a decision an author should have to make, and offering it would be a
    /// mistake: the public site chooses the right size per device from the whole
    /// group anyway (see <c>ResponsiveImageResolutionService</c>).
    /// <para>
    /// The Desktop slot represents the group when one is set — it is the size an
    /// author placing an image in a page is thinking of. Older groups uploaded before
    /// slots existed fall back to the widest variant, which was the previous rule.
    /// Either way this is the safest single URL to put in a page: correct on its own
    /// if the srcset never resolves, and the one the builder's own canvas should show
    /// while editing.
    /// </para>
    /// </summary>
    public static List<MediaPickerAsset> ToPickerAssets(IEnumerable<MediaFileResponse> files) =>
        [.. files
            .Select(m => new MediaPickerAsset(
                m.FilePath,
                m.Title.IsNullOrWhiteSpace() ? m.OriginalFileName : m.Title!,
                m.AltText ?? string.Empty))];
}
