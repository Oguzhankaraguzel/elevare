namespace SharedKernel.Social;

/// <summary>
/// Reads account handles out of the profile links in Site Settings. Shared so the
/// public site's twitter:site and the CMS's "left empty, this is used" hint are
/// always the same value.
/// </summary>
public static class SocialProfiles
{
    private static readonly HashSet<string> XHosts = new(StringComparer.OrdinalIgnoreCase)
    {
        "x.com", "www.x.com", "twitter.com", "www.twitter.com", "mobile.twitter.com",
    };

    /// <summary>"https://x.com/example" → "@example". Null for anything else.</summary>
    public static string? XHandle(string? profileLink)
    {
        if (!Uri.TryCreate(profileLink, UriKind.Absolute, out Uri? uri) || !XHosts.Contains(uri.Host))
            return null;
        string handle = uri.AbsolutePath.Trim('/').Split('/')[0];
        return string.IsNullOrWhiteSpace(handle) ? null : "@" + handle.TrimStart('@');
    }
}
