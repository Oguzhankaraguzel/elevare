using SharedKernel.Concrete;

namespace Application.Abstraction.Services.Files;

/// <summary>
/// Makes the sized favicon set — 32, 48, 180 (Apple touch), 192 and 512 pixels, as
/// PNG — from whatever image Site Settings names as the favicon, so the public page
/// can offer each device the size it asks for instead of one file at one size.
/// <para>
/// The trigger was a 67 KB .ico served for a 36-pixel tab icon. A browser fetches
/// the icon on every first visit; a PNG at the right size is about 2 KB.
/// </para>
/// </summary>
public interface IFaviconService
{
    /// <summary>Pixel sizes in the generated set; each is <c>uploads/favicons/favicon-{size}.png</c>.</summary>
    static readonly int[] Sizes = [32, 48, 180, 192, 512];

    /// <summary>
    /// Regenerates the set from the media file at <paramref name="sourcePath"/>.
    /// Success with <c>false</c> when the source is not an image the set can be made
    /// from (an SVG, a file outside the library) — that is a valid favicon, just not
    /// one to resize; the page then keeps serving the source as before.
    /// </summary>
    Task<Result<bool>> RegenerateAsync(string? sourcePath, CancellationToken cancellationToken = default);
}
