using SharedKernel.Concrete;

namespace Domain.Entities.SiteCodeSnippets;

/// <summary>
/// Why a snippet was refused. Every message names the thing that is wrong and what
/// to do instead — the person pasting a tag is usually not the person who wrote it,
/// so "invalid input" would leave them with nowhere to go.
/// </summary>
public static class SiteCodeSnippetErrors
{
    public static readonly Error NotFound =
        Error.NotFound("SiteCode.NotFound", "The code snippet was not found.");

    public static readonly Error NameRequired =
        Error.Failure("SiteCode.NameRequired", "Give the snippet a name so it can be recognised in the list later.");

    public static readonly Error ContentRequired =
        Error.Failure("SiteCode.ContentRequired", "There is no code to save. Paste the snippet your provider gave you.");

    /// <summary>Unclosed tags. An unterminated &lt;script&gt; swallows the rest of the page.</summary>
    public static readonly Error Malformed =
        Error.Failure("SiteCode.Malformed",
            "This code has an unclosed tag. Copy the whole snippet from your provider — a missing closing tag can blank out the entire site.");

    /// <summary>A whole HTML document was pasted instead of the snippet from it.</summary>
    public static readonly Error WholeDocumentPasted =
        Error.Failure("SiteCode.WholeDocumentPasted",
            "This looks like a complete web page, not a snippet. Paste only the part your provider marked for the head or body.");

    public static readonly Error TooLarge =
        Error.Failure("SiteCode.TooLarge",
            "The snippet is too large. Host big libraries as a file and reference them with a <script src=\"...\"> tag instead of pasting the whole thing.");

    public static Error KindMismatch(string expected) =>
        Error.Failure("SiteCode.KindMismatch",
            $"The selected type does not match the code. Expected {expected}. Change the type, or paste the matching snippet.");

    public static readonly Error PresetValueUnrecognised =
        Error.Failure("SiteCode.PresetValueUnrecognised",
            "The id could not be read from what was pasted. Paste either the id on its own or the complete snippet from the provider's setup page.");
}
