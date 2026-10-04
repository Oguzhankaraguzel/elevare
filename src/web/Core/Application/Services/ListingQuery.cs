namespace Application.Services;

/// <summary>
/// The query-string parameters page listings read — and the only ones a page
/// reacts to. The first listing on a page uses <c>page</c>, <c>tag</c> and
/// <c>q</c>; every further one the same names suffixed with its block id
/// (<c>page-ilist2</c>), so paging one listing does not page the others.
/// <para>
/// Everything else in a URL — utm_*, fbclid, a typo — is ignored: it is not carried
/// into the listing's links or its search form, not written into the canonical
/// address, and not part of the output-cache key (see the "Pages" policy), so a
/// tracking parameter can neither duplicate a page for search engines nor mint a
/// new cache entry per visitor.
/// </para>
/// </summary>
public static class ListingQuery
{
    public const string PageKey = "page";
    public const string TagKey = "tag";
    public const string SearchKey = "q";

    /// <summary>Longest search term honoured; anything longer is cut, not rejected.</summary>
    public const int MaxSearchLength = 100;

    private static readonly string[] BaseKeys = [PageKey, TagKey, SearchKey];

    /// <summary>The parameter <paramref name="baseKey"/> of the listing with <paramref name="suffix"/> ("" for the first).</summary>
    public static string Key(string baseKey, string suffix) =>
        suffix.Length == 0 ? baseKey : $"{baseKey}-{suffix}";

    /// <summary>True for a parameter some listing reads.</summary>
    public static bool IsListingKey(string key)
    {
        foreach (string baseKey in BaseKeys)
        {
            if (string.Equals(key, baseKey, StringComparison.Ordinal))
                return true;
            if (key.Length > baseKey.Length + 1
                && key.StartsWith(baseKey + "-", StringComparison.Ordinal)
                && key.AsSpan(baseKey.Length + 1).IndexOfAnyExcept(AllowedSuffixChars) < 0)
                return true;
        }
        return false;
    }

    /// <summary>Only the listing parameters of <paramref name="query"/>, empty values kept (an empty tag means "all").</summary>
    public static Dictionary<string, string?> Filter(IEnumerable<KeyValuePair<string, string?>> query)
    {
        ArgumentNullException.ThrowIfNull(query);
        Dictionary<string, string?> kept = new(StringComparer.Ordinal);
        foreach ((string key, string? value) in query)
        {
            if (IsListingKey(key))
                kept[key] = value;
        }
        return kept;
    }

    /// <summary>A stable text form of the listing parameters, for the output-cache key.</summary>
    public static string CacheKey(IEnumerable<KeyValuePair<string, string?>> query) =>
        string.Join('&', Filter(query)
            .OrderBy(kv => kv.Key, StringComparer.Ordinal)
            .Select(kv => kv.Key + "=" + kv.Value));

    private static readonly System.Buffers.SearchValues<char> AllowedSuffixChars =
        System.Buffers.SearchValues.Create("abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789_-");
}
