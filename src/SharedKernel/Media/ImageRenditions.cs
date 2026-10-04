using System.Text.Json;
using System.Text.Json.Serialization;

namespace SharedKernel.Media;
/// <summary>
/// The derived copies of a media library image, kept on the image's own row as one
/// JSON column (<c>Renditions</c>) rather than as rows of their own. They were rows
/// for a short while: one upload then meant ten library entries, the "delete"
/// button removed one of them, and the library screen had to fold the other nine
/// away. A rendition is not a file anyone chooses in the library — it is how the
/// public site serves the one file that was chosen — so it belongs to that file's
/// record, and follows it through delete, restore and purge without any code
/// remembering to.
/// <para>
/// Shared between the CMS (which writes it at upload) and the public site (which
/// reads it to build <c>srcset</c>), so neither can drift from the other.
/// </para>
/// </summary>
public static class ImageRenditions
{
    private static readonly JsonSerializerOptions Options = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>Null when there is nothing to store, so the column stays NULL rather than "[]".</summary>
    public static string? ToJson(IReadOnlyList<ImageRendition> items) =>
        items.Count == 0 ? null : JsonSerializer.Serialize(items, Options);

    /// <summary>Empty for a NULL column or anything that is not the expected shape — never throws.</summary>
    public static IReadOnlyList<ImageRendition> Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try
        {
            List<ImageRendition>? items = JsonSerializer.Deserialize<List<ImageRendition>>(json, Options);
            return items is null ? [] : [.. items.Where(i => i.Width > 0 && i.Height > 0).OrderBy(i => i.Width)];
        }
        catch (JsonException)
        {
            return [];
        }
    }
}
