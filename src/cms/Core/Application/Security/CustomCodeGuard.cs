using System.Text.RegularExpressions;

namespace Application.Security;

/// <summary>
/// Detects raw script / custom-code usage in GrapeJS-authored HTML (the
/// <c>grapesjs-custom-code</c> plugin lets editors paste arbitrary markup). Content
/// matching this is gated behind
/// <see cref="Application.Abstraction.Services.Authentication.IUserContext.CanAuthorCustomCode"/>
/// so that ordinary content roles can't inject sitewide scripts, while trusted
/// roles (SuperAdmin/Admin/Developer) can still embed analytics, chat widgets, etc.
/// </summary>
public static partial class CustomCodeGuard
{
    public static bool ContainsCustomCode(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
            return false;

        return ScriptTagRegex().IsMatch(html)
            || InlineEventHandlerRegex().IsMatch(html)
            || JavascriptProtocolRegex().IsMatch(html);
    }

    [GeneratedRegex(@"<script\b", RegexOptions.IgnoreCase)]
    private static partial Regex ScriptTagRegex();

    [GeneratedRegex(@"\son\w+\s*=", RegexOptions.IgnoreCase)]
    private static partial Regex InlineEventHandlerRegex();

    [GeneratedRegex(@"(?:href|src)\s*=\s*[""']?\s*javascript:", RegexOptions.IgnoreCase)]
    private static partial Regex JavascriptProtocolRegex();
}
