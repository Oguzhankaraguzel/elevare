using Application.Features.Queries.Media.GetMediaFiles;
using SharedKernel.Extensions.Strings;

namespace Wasm.Models.Media;

/// <summary>
/// One entry in GrapesJS's "Select Image" dialog. Shape read by grapes-editor.js's
/// <c>refreshAssets</c> — property names are lower-cased on the JS side.
/// </summary>
/// <param name="Src">
/// Site-relative serving path (<c>/uploads/images/…</c>), named after the attribute
/// it ends up in. Deliberately not absolute: the same path resolves against the CMS
/// inside the builder's canvas and against the public site once the page is
/// published, so one stored URL is correct in both places and survives a domain
/// change.
/// </param>
/// <param name="Name">Label under the thumbnail.</param>
/// <param name="Alt">
/// The library's alt text, applied to the image when it is picked. Empty when the
/// library has none — the author is then prompted by the SEO panel instead of being
/// given a wrong description.
/// </param>
public sealed record MediaPickerAsset(string Src, string Name, string Alt);
