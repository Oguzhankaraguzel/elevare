namespace SharedKernel.Forms;

/// <summary>
/// What a visitor may attach to a public form, decided once for both applications:
/// the public site enforces it on the way in, the CMS quotes it (block hint,
/// Knowledge Base) so the author and the visitor read the same limits.
/// <para>
/// The list is short on purpose. A public, unauthenticated upload endpoint is a
/// target; every type admitted is a type someone will probe. These five cover
/// the forms a company site actually has — a CV, a photo, a brief, a screenshot —
/// and each is recognised by its <b>content</b> (the leading bytes), never by the
/// file name a browser was told.
/// </para>
/// </summary>
public static class FormAttachmentPolicy
{
    public const int MaxFiles = 3;
    public const long MaxFileBytes = 10L * 1024 * 1024;

    public sealed record Kind(string Extension, string ContentType, string Label);

    public static readonly IReadOnlyList<Kind> Allowed =
    [
        new(".pdf", "application/pdf", "PDF"),
        new(".jpg", "image/jpeg", "JPG"),
        new(".png", "image/png", "PNG"),
        new(".webp", "image/webp", "WebP"),
        new(".docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document", "DOCX"),
    ];

    /// <summary>The <c>accept</c> attribute value for a file input under this policy.</summary>
    public static string AcceptAttribute => string.Join(",", Allowed.Select(k => k.Extension).Append(".jpeg"));

    /// <summary>"PDF, JPG, PNG, WebP, DOCX" — for hints.</summary>
    public static string LabelList => string.Join(", ", Allowed.Select(k => k.Label));

    /// <summary>
    /// The kind the bytes actually are, or null when they are none of the allowed
    /// ones. The declared name and content type are not consulted: a renamed
    /// executable is refused on its first bytes, and a JPEG called "cv.pdf" is
    /// stored as the JPEG it is.
    /// </summary>
    public static Kind? Detect(ReadOnlySpan<byte> head)
    {
        if (head.Length >= 5 && head[0] == 0x25 && head[1] == 0x50 && head[2] == 0x44 && head[3] == 0x46 && head[4] == 0x2D) // %PDF-
            return Allowed[0];
        if (head.Length >= 3 && head[0] == 0xFF && head[1] == 0xD8 && head[2] == 0xFF)
            return Allowed[1];
        if (head.Length >= 8 && head[0] == 0x89 && head[1] == 0x50 && head[2] == 0x4E && head[3] == 0x47
            && head[4] == 0x0D && head[5] == 0x0A && head[6] == 0x1A && head[7] == 0x0A)
            return Allowed[2];
        if (head.Length >= 12 && head[0] == 0x52 && head[1] == 0x49 && head[2] == 0x46 && head[3] == 0x46
            && head[8] == 0x57 && head[9] == 0x45 && head[10] == 0x42 && head[11] == 0x50) // RIFF....WEBP
            return Allowed[3];
        // A .docx is a ZIP. The container signature alone would admit any ZIP —
        // an .xlsx, an archive — so the head is also searched for the manifest
        // entry every Office file carries and for the "word/" part only a Word
        // document has (see LooksLikeDocx).
        if (head.Length >= 4 && head[0] == 0x50 && head[1] == 0x4B && head[2] == 0x03 && head[3] == 0x04 && LooksLikeDocx(head))
            return Allowed[4];
        return null;
    }

    // The entry names in a ZIP's local file headers are stored uncompressed even
    // when their contents are not: a real .docx opens with "[Content_Types].xml"
    // and reaches a "word/…" entry within its first few kilobytes. Both names are
    // looked for in the head we were given (DetectHeadBytes is sized for it).
    private static bool LooksLikeDocx(ReadOnlySpan<byte> head)
    {
        ReadOnlySpan<byte> contentTypes = "[Content_Types].xml"u8;
        ReadOnlySpan<byte> wordPart = "word/"u8;
        return head.IndexOf(contentTypes) >= 0 && head.IndexOf(wordPart) >= 0;
    }

    /// <summary>How many leading bytes <see cref="Detect"/> wants to see.</summary>
    public const int DetectHeadBytes = 16384;

    /// <summary>
    /// A file name safe to store and to show back: no path, no control characters,
    /// bounded length, and the extension the bytes say — not the one the name did.
    /// </summary>
    public static string SafeFileName(string? declared, Kind kind)
    {
        // Both separators by hand: Path.GetFileName only knows the host's, and a
        // Windows browser may send "C:\...\cv.pdf" to a server running on Linux.
        string name = declared ?? "";
        int cut = Math.Max(name.LastIndexOf('/'), name.LastIndexOf('\\'));
        if (cut >= 0) name = name[(cut + 1)..];
        name = Path.GetFileNameWithoutExtension(name);
        string cleaned = new string(name.Where(c => !char.IsControl(c) && c != '/' && c != '\\' && c != ':').ToArray()).Trim();
        if (cleaned.Length == 0) cleaned = "dosya";
        if (cleaned.Length > 120) cleaned = cleaned[..120];
        return cleaned + kind.Extension;
    }
}
