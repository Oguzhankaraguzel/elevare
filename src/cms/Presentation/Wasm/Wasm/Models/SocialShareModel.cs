using SharedKernel.Social;

namespace Wasm.Models;

/// <summary>
/// The page editor's working copy of everything on the "Sosyal Paylaşım" panel:
/// the five core Open Graph fields and the two X fields the page stores in its own
/// columns, plus the extended <see cref="SocialMeta"/> document. One object so the
/// panel component edits it in place and the editor reads it back for the save and
/// for the unsaved-changes signature.
/// </summary>
public sealed class SocialShareModel
{
    public string OgTitle { get; set; } = "";
    public string OgDescription { get; set; } = "";
    public string OgType { get; set; } = "website";
    public string? OgImage { get; set; }
    public string? OgUrl { get; set; }
    public string? TwitterCard { get; set; }
    public string? TwitterSite { get; set; }
    public SocialMeta Meta { get; set; } = new();

    /// <summary>Everything, flattened, for the editor's dirty-check signature.</summary>
    public string Signature() =>
        string.Join('', OgTitle, OgDescription, OgType, OgImage, OgUrl, TwitterCard, TwitterSite, Meta.ToJson());
}
