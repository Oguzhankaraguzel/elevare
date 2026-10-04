using System.Text.RegularExpressions;

namespace Application.Features.Commands.Pages;

/// <summary>
/// Rewrites the site's own page addresses wherever they are written down — a link in
/// a page or template, og:url, a structured data <c>@id</c> — after pages have moved.
/// <para>
/// A move is old FullSlug → new FullSlug ("en/about" → "about"). Every spelling of
/// the old address is caught: absolute with or without "www." and over http or
/// https (<c>https://www.example.com/en/about</c>), root-relative
/// (<c>/en/about</c>), and bare inside a link attribute (<c>href="en/about"</c>);
/// a trailing "/" is tolerated and a "#fragment" or "?query" is kept. An address
/// only matches whole: <c>/en/about-us</c> and <c>/en/about/team</c> are left
/// alone unless they moved themselves.
/// </para>
/// <para>
/// The homepage's old address is "" — the bare site root — and that is written in
/// places that do not mean "the homepage": the Organization's url is the site
/// itself, a breadcrumb separator is a lone "/". So the root is only moved where
/// it is unmistakably a link to the homepage: with its trailing slash in an
/// absolute address (the Organization writes it without one), or as the value of
/// a link attribute.
/// </para>
/// </summary>
public sealed class SiteAddressRewriter
{
    // What may follow an address: the end of a quoted value, a fragment or query, or
    // the markup around it. Not "/", or "/en" would match the start of "/en/about".
    private const string End = """(?=$|["'#?\s<>),\\&])""";

    private readonly Regex _pattern;
    private readonly Dictionary<string, string> _moves;

    private SiteAddressRewriter(Regex pattern, Dictionary<string, string> moves)
    {
        _pattern = pattern;
        _moves = moves;
    }

    /// <param name="moves">Old FullSlug → new FullSlug.</param>
    /// <param name="siteBaseUrl">The public site's address; absolute links are only recognised on its host.</param>
    /// <returns>Null when nothing moved.</returns>
    public static SiteAddressRewriter? Create(IReadOnlyDictionary<string, string> moves, Uri? siteBaseUrl)
    {
        Dictionary<string, string> map = new(StringComparer.OrdinalIgnoreCase);
        foreach ((string from, string to) in moves)
        {
            string old = from.Trim('/');
            if (!string.Equals(old, to.Trim('/'), StringComparison.OrdinalIgnoreCase))
                map[old] = to.Trim('/');
        }
        if (map.Count == 0)
            return null;

        bool homeMoved = map.ContainsKey("");
        string paths = string.Join("|", map.Keys
            .Where(k => k.Length > 0)
            .OrderByDescending(k => k.Length)
            .Select(Regex.Escape));

        List<string> forms = [];
        if (HostOf(siteBaseUrl) is string host)
        {
            // The homepage's "" as one more alternative, so "https://host/" matches too.
            string any = homeMoved && paths.Length > 0 ? paths + "|" : paths;
            forms.Add($@"(?<abs>https?://(?:www\.)?{Regex.Escape(host)})/(?<path>{any})/?{End}");
        }
        if (paths.Length > 0)
        {
            forms.Add($"""(?<=^|["'=(\s,>])/(?<path>{paths})/?{End}""");
            forms.Add($"""(?<=\b(?:href|src|action)=\\?["'])(?<bare>{paths})/?{End}""");
        }
        if (homeMoved)
            forms.Add($"""(?<=\b(?:href|action)=\\?["']|"(?:href|url)"\s*:\s*")/(?<path>){End}""");

        Regex pattern = new(string.Join("|", forms),
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(5));
        return new SiteAddressRewriter(pattern, map);
    }

    /// <summary>The text with every moved address replaced; the same instance when nothing matched.</summary>
    public string? Rewrite(string? text) =>
        string.IsNullOrEmpty(text) ? text : _pattern.Replace(text, Replace);

    private string Replace(Match m)
    {
        Group bare = m.Groups["bare"];
        if (bare.Success)
        {
            string target = _moves[bare.Value];
            return target.Length == 0 ? "/" : target;
        }

        string moved = "/" + _moves[m.Groups["path"].Value];
        Group abs = m.Groups["abs"];
        return abs.Success ? abs.Value + moved : moved;
    }

    private static string? HostOf(Uri? baseUrl)
    {
        if (baseUrl is not { IsAbsoluteUri: true })
            return null;
        string host = baseUrl.Authority;
        return host.StartsWith("www.", StringComparison.OrdinalIgnoreCase) ? host[4..] : host;
    }
}
