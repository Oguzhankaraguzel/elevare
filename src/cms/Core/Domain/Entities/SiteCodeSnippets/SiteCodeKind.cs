namespace Domain.Entities.SiteCodeSnippets;

/// <summary>
/// What a snippet is meant to contain. Declaring it is what lets the validator say
/// "you picked Script but pasted a stylesheet" instead of silently accepting markup
/// that will not do what the author expects.
/// </summary>
public enum SiteCodeKind
{
    /// <summary>One or more <c>&lt;script&gt;</c> tags.</summary>
    Script = 1,

    /// <summary><c>&lt;style&gt;</c> blocks or <c>&lt;link rel="stylesheet"&gt;</c> — a consent banner's CSS, for instance.</summary>
    Style,

    /// <summary>A single <c>&lt;meta&gt;</c> tag, such as a search-console verification.</summary>
    MetaTag,

    /// <summary>Anything else (a <c>&lt;noscript&gt;</c> iframe, a verification <c>&lt;link&gt;</c>).</summary>
    RawHtml
}
