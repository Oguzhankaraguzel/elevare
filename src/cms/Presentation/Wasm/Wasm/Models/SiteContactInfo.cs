using Application.Features.Queries.SiteSettings.GetSiteSettings;

namespace Wasm.Models;

/// <summary>
/// The contact and social details from Site Settings, in the shape the page
/// builder's blocks read them (see <c>siteValue</c> in elevare-blocks.js).
/// <para>
/// Both editors need exactly this set, and a block that quietly ships a sample
/// address or a social icon pointing at "#" is worse than one with no block at
/// all: it looks finished. Everything here already exists on the Site Settings
/// screen — this is only the wire between the two.
/// </para>
/// </summary>
public sealed record SiteContactInfo(
    string Phone,
    string Email,
    string Address,
    string AddressLocality,
    string AddressRegion,
    string PostalCode,
    string AddressCountry,
    string WorkingHours,
    string Facebook,
    string Instagram,
    string X,
    string LinkedIn,
    string Youtube,
    string Whatsapp,
    string GoogleNews,
    string GooglePlaceId)
{
    public static SiteContactInfo Empty { get; } =
        new("", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "");

    public static SiteContactInfo From(IReadOnlyCollection<SiteSettingResponse> settings)
    {
        string Get(string key) =>
            settings.FirstOrDefault(s => s.Key == key)?.Value?.Trim() ?? "";

        return new SiteContactInfo(
            Phone: Get("Contact.Phone"),
            Email: Get("Contact.Email"),
            Address: Get("Contact.Address"),
            AddressLocality: Get("Contact.AddressLocality"),
            AddressRegion: Get("Contact.AddressRegion"),
            PostalCode: Get("Contact.PostalCode"),
            AddressCountry: Get("Contact.AddressCountry"),
            WorkingHours: Get("Contact.WorkingHours"),
            Facebook: Get("Social.FacebookUrl"),
            Instagram: Get("Social.InstagramUrl"),
            X: Get("Social.XUrl"),
            LinkedIn: Get("Social.LinkedInUrl"),
            Youtube: Get("Social.YoutubeUrl"),
            Whatsapp: Get("Social.WhatsappNumber"),
            GoogleNews: Get("Social.GoogleNewsUrl"),
            GooglePlaceId: Get("Contact.GooglePlaceId"));
    }
}
