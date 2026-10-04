using Domain.Entities.SiteSettings;

namespace Application.Security;

/// <summary>
/// Checks a hand-written robots.txt before it starts answering crawlers.
/// <para>
/// The failure mode here is quiet: a robots.txt is never "broken" in a way a browser
/// shows. It just gets obeyed, and a site can drop out of search results for weeks
/// before anyone connects it to a line someone typed. So one structural mistake is
/// refused outright, and the rest are surfaced as warnings the author can accept.
/// </para>
/// </summary>
public static class RobotsTxtValidator
{
    /// <summary>Google stops reading at 500 KB; nothing legitimate comes close.</summary>
    public const int MaxContentLength = 64 * 1024;

    /// <summary>Directives crawlers actually act on. Anything else is dead text.</summary>
    private static readonly HashSet<string> KnownDirectives = new(StringComparer.OrdinalIgnoreCase)
    {
        "user-agent", "disallow", "allow", "sitemap", "crawl-delay", "host", "clean-param",
    };

    public static RobotsTxtValidationResult Validate(string? content)
    {
        // Empty is a valid choice, not a mistake: it means "use the built-in default".
        if (string.IsNullOrWhiteSpace(content))
            return RobotsTxtValidationResult.Valid([]);

        if (content.Length > MaxContentLength)
            return RobotsTxtValidationResult.Invalid(SeoErrors.RobotsTooLarge);

        string[] lines = content.ReplaceLineEndings("\n").Split('\n');

        List<string> warnings = [];
        bool hasUserAgent = false;
        bool blocksEverything = false;
        string currentAgent = "";

        foreach (string raw in lines)
        {
            string line = raw.Trim();

            if (line.Length == 0 || line.StartsWith('#'))
                continue;

            int separator = line.IndexOf(':', StringComparison.Ordinal);
            if (separator <= 0)
            {
                warnings.Add($"UnparsableLine:{Shorten(line)}");
                continue;
            }

            string directive = line[..separator].Trim();
            string value = line[(separator + 1)..].Trim();

            if (!KnownDirectives.Contains(directive))
            {
                warnings.Add($"UnknownDirective:{Shorten(directive)}");
                continue;
            }

            if (directive.Equals("user-agent", StringComparison.OrdinalIgnoreCase))
            {
                hasUserAgent = true;
                currentAgent = value;
            }
            else if (directive.Equals("disallow", StringComparison.OrdinalIgnoreCase)
                && value == "/"
                && currentAgent == "*")
            {
                blocksEverything = true;
            }
        }

        // Every rule belongs to the User-agent above it. With none, the file parses
        // cleanly and does nothing at all — the one case worth refusing, because it
        // looks exactly like a working file.
        if (!hasUserAgent)
            return RobotsTxtValidationResult.Invalid(SeoErrors.RobotsMissingUserAgent);

        if (blocksEverything)
            warnings.Add("BlocksEntireSite");

        return RobotsTxtValidationResult.Valid(warnings);
    }

    private static string Shorten(string value) =>
        value.Length <= 40 ? value : value[..40] + "…";
}
