using System.Text;

namespace SharedKernel.Content;

/// <summary>
/// Colours source code for the "Kod Bloğu" block, on the server, so a page with code
/// on it ships no highlighting script at all. The public site runs it on every code
/// block it renders; the CMS editor asks it too, so the canvas shows the same colours. Deliberately a lexer, not a parser:
/// comments, strings, numbers, keywords, types and calls are what a reader's eye
/// uses, and a small lexer gets those right for every language offered here without
/// a dependency. Anything it does not recognise is written out plain — never lost.
/// Output is HTML: every character of the code is escaped, tokens are wrapped in
/// <c>&lt;span class="tk-…"&gt;</c> (the public site's <c>ContentEnhancer</c> holds the colours).
/// </summary>
public static class CodeHighlighter
{
    /// <summary>The languages offered, by the value the block stores, with their labels.</summary>
    public static readonly IReadOnlyDictionary<string, string> Languages = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["csharp"] = "C#", ["javascript"] = "JavaScript", ["typescript"] = "TypeScript", ["json"] = "JSON",
        ["html"] = "HTML", ["xml"] = "XML", ["css"] = "CSS", ["sql"] = "SQL", ["bash"] = "Bash",
        ["powershell"] = "PowerShell", ["python"] = "Python", ["plaintext"] = "Metin",
    };

    public static string Highlight(string code, string? language)
    {
        ArgumentNullException.ThrowIfNull(code);
        string lang = Normalize(language);
        var sb = new StringBuilder(code.Length * 2);
        switch (lang)
        {
            case "html":
            case "xml":
                Markup(code, sb);
                break;
            case "css":
                Css(code, sb);
                break;
            case "plaintext":
                Encode(code, sb);
                break;
            default:
                Generic(code, Grammars[lang], lang == "json", sb);
                break;
        }
        return sb.ToString();
    }

    /// <summary>The block's stored language, or an alias of one, as a key of <see cref="Languages"/>.</summary>
    public static string Normalize(string? language)
    {
        string v = (language ?? "").Trim().ToUpperInvariant();
        return v switch
        {
            "CSHARP" or "CS" or "C#" => "csharp",
            "JAVASCRIPT" or "JS" => "javascript",
            "TYPESCRIPT" or "TS" => "typescript",
            "JSON" => "json",
            "HTML" or "CSHTML" or "RAZOR" => "html",
            "XML" or "XAML" or "CSPROJ" => "xml",
            "CSS" => "css",
            "SQL" => "sql",
            "BASH" or "SH" or "SHELL" => "bash",
            "POWERSHELL" or "PS1" or "PWSH" => "powershell",
            "PYTHON" or "PY" => "python",
            _ => "plaintext",
        };
    }

    // ── Generic C-like / scripting lexer ─────────────────────────────────────

    private sealed record Grammar(
        string[] LineComments,
        (string Open, string Close)[] BlockComments,
        string Quotes,
        HashSet<string> Keywords,
        bool CaseInsensitive = false,
        bool TypesByCase = false,
        bool Variables = false);

    private static HashSet<string> Words(string words, bool caseInsensitive = false) =>
        new(words.Split(' ', StringSplitOptions.RemoveEmptyEntries),
            caseInsensitive ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);

    private const string JsWords = "break case catch class const continue debugger default delete do else export extends finally for function if import in instanceof let new return super switch this throw try typeof var void while with yield async await of static get set true false null undefined";

    private static readonly Dictionary<string, Grammar> Grammars = new(StringComparer.Ordinal)
    {
        ["csharp"] = new(["//"], [("/*", "*/")], "\"'",
            Words("abstract as base bool break byte case catch char checked class const continue decimal default delegate do double else enum event explicit extern false finally fixed float for foreach goto if implicit in int interface internal is lock long namespace new null object operator out override params private protected public readonly ref return sbyte sealed short sizeof stackalloc static string struct switch this throw true try typeof uint ulong unchecked unsafe ushort using virtual void volatile while var async await get set init record required yield when where nameof dynamic partial with and or not file nint nuint"),
            TypesByCase: true),
        ["javascript"] = new(["//"], [("/*", "*/")], "\"'`", Words(JsWords), TypesByCase: true),
        ["typescript"] = new(["//"], [("/*", "*/")], "\"'`",
            Words(JsWords + " interface type enum implements private public protected readonly declare namespace abstract as keyof infer any string number boolean never unknown"),
            TypesByCase: true),
        ["json"] = new([], [], "\"", Words("true false null")),
        ["sql"] = new(["--"], [("/*", "*/")], "'",
            Words("select from where insert into values update set delete create table alter drop index view join inner left right outer full cross on and or not null is as order by group having limit offset distinct union all case when then else end primary key foreign references default exists in like between top with begin commit rollback transaction declare int integer bigint smallint varchar nvarchar char text boolean bit date datetime datetime2 timestamp decimal numeric float real true false asc desc count sum avg min max", caseInsensitive: true),
            CaseInsensitive: true),
        ["bash"] = new(["#"], [], "\"'",
            Words("if then else elif fi for while until do done case esac function in return export local readonly echo exit cd sudo source alias unset shift true false"),
            Variables: true),
        ["powershell"] = new(["#"], [("<#", "#>")], "\"'",
            Words("if else elseif switch foreach for while do until function param return break continue try catch finally throw begin process end in true false null", caseInsensitive: true),
            CaseInsensitive: true, Variables: true),
        ["python"] = new(["#"], [], "\"'",
            Words("False None True and as assert async await break class continue def del elif else except finally for from global if import in is lambda nonlocal not or pass raise return try while with yield self"),
            TypesByCase: true),
    };

    private static void Generic(string s, Grammar g, bool json, StringBuilder sb)
    {
        int i = 0;
        while (i < s.Length)
        {
            string? line = g.LineComments.FirstOrDefault(p => At(s, i, p));
            if (line is not null)
            {
                int end = s.IndexOf('\n', i);
                i = Token(s, i, end < 0 ? s.Length : end, "c", sb);
                continue;
            }
            (string Open, string Close) block = g.BlockComments.FirstOrDefault(b => At(s, i, b.Open));
            if (block.Open is not null)
            {
                int end = s.IndexOf(block.Close, i + block.Open.Length, StringComparison.Ordinal);
                i = Token(s, i, end < 0 ? s.Length : end + block.Close.Length, "c", sb);
                continue;
            }

            char c = s[i];
            int stringEnd = StringEnd(s, i, g.Quotes);
            if (stringEnd > i)
            {
                // JSON: a string followed by ':' is a key.
                string kind = json && NextNonSpace(s, stringEnd) == ':' ? "p" : "s";
                i = Token(s, i, stringEnd, kind, sb);
                continue;
            }
            if (char.IsDigit(c) || c == '.' && i + 1 < s.Length && char.IsDigit(s[i + 1]))
            {
                i = Token(s, i, NumberEnd(s, i), "n", sb);
                continue;
            }
            if (g.Variables && c == '$' && i + 1 < s.Length && (char.IsLetter(s[i + 1]) || s[i + 1] == '_' || s[i + 1] == '{'))
            {
                int end = i + 1;
                if (s[end] == '{')
                {
                    int close = s.IndexOf('}', end);
                    end = close < 0 ? s.Length : close + 1;
                }
                else
                {
                    while (end < s.Length && (char.IsLetterOrDigit(s[end]) || s[end] == '_')) end++;
                }
                i = Token(s, i, end, "p", sb);
                continue;
            }
            if (char.IsLetter(c) || c == '_' || c == '@' && i + 1 < s.Length && char.IsLetter(s[i + 1]))
            {
                int end = i + 1;
                while (end < s.Length && (char.IsLetterOrDigit(s[end]) || s[end] == '_')) end++;
                string word = s[i..end];
                string bare = word.TrimStart('@');
                string? kind = null;
                if (g.Keywords.Contains(bare)) kind = "k";
                else if (NextNonSpace(s, end) == '(') kind = "f";
                else if (g.TypesByCase && char.IsUpper(bare[0])) kind = "t";
                i = kind is null ? Plain(s, i, end, sb) : Token(s, i, end, kind, sb);
                continue;
            }
            Encode(c, sb);
            i++;
        }
    }

    // A quoted string starting at i — with C#'s verbatim/interpolated/raw forms and
    // Python's triple quotes — or i itself when there is none.
    private static int StringEnd(string s, int i, string quotes)
    {
        int start = i;
        while (i < s.Length && (s[i] == '$' || s[i] == '@') && i - start < 3) i++;
        bool verbatim = s.AsSpan(start, i - start).Contains('@');
        if (i >= s.Length || quotes.IndexOf(s[i]) < 0 || i > start && s[i] != '"')
            return start;
        char q = s[i];
        // """raw""" (C# 11) and Python's ''' / """.
        if (At(s, i, new string(q, 3)))
        {
            int close = s.IndexOf(new string(q, 3), i + 3, StringComparison.Ordinal);
            return close < 0 ? s.Length : close + 3;
        }
        int j = i + 1;
        while (j < s.Length)
        {
            char c = s[j];
            if (!verbatim && c == '\\') { j += 2; continue; }
            if (c == q)
            {
                if (verbatim && j + 1 < s.Length && s[j + 1] == q) { j += 2; continue; }
                return j + 1;
            }
            // Only template literals and verbatim strings run past the line.
            if (c == '\n' && q != '`' && !verbatim) return j;
            j++;
        }
        return s.Length;
    }

    private static int NumberEnd(string s, int i)
    {
        int j = i;
        if (At(s, i, "0x") || At(s, i, "0X") || At(s, i, "0b") || At(s, i, "0B")) j += 2;
        while (j < s.Length && (char.IsLetterOrDigit(s[j]) || s[j] == '_' || s[j] == '.'
            || (s[j] == '+' || s[j] == '-') && (s[j - 1] == 'e' || s[j - 1] == 'E')))
        {
            if (s[j] == '.' && (j + 1 >= s.Length || !char.IsDigit(s[j + 1]))) break;
            j++;
        }
        return j;
    }

    // ── Markup (HTML, XML) ───────────────────────────────────────────────────

    private static void Markup(string s, StringBuilder sb)
    {
        int i = 0;
        while (i < s.Length)
        {
            if (At(s, i, "<!--"))
            {
                int end = s.IndexOf("-->", i + 4, StringComparison.Ordinal);
                i = Token(s, i, end < 0 ? s.Length : end + 3, "c", sb);
                continue;
            }
            if (s[i] == '<' && i + 1 < s.Length && (char.IsLetter(s[i + 1]) || s[i + 1] == '/' || s[i + 1] == '!' || s[i + 1] == '?'))
            {
                int nameEnd = i + 1;
                while (nameEnd < s.Length && !char.IsWhiteSpace(s[nameEnd]) && s[nameEnd] != '>' && !(s[nameEnd] == '/' && nameEnd > i + 1)) nameEnd++;
                i = Token(s, i, nameEnd, "g", sb);
                // Attributes up to the closing '>'.
                while (i < s.Length && s[i] != '>')
                {
                    if (s[i] == '"' || s[i] == '\'')
                    {
                        int close = s.IndexOf(s[i], i + 1);
                        i = Token(s, i, close < 0 ? s.Length : close + 1, "s", sb);
                    }
                    else if (char.IsLetter(s[i]) || s[i] == '_' || s[i] == ':' || s[i] == '@')
                    {
                        int end = i + 1;
                        while (end < s.Length && (char.IsLetterOrDigit(s[end]) || "-_:.@".Contains(s[end]))) end++;
                        i = Token(s, i, end, "p", sb);
                    }
                    else if (s[i] == '/' || s[i] == '?')
                    {
                        i = Token(s, i, i + 1, "g", sb);
                    }
                    else
                    {
                        Encode(s[i], sb);
                        i++;
                    }
                }
                if (i < s.Length) i = Token(s, i, i + 1, "g", sb);
                continue;
            }
            Encode(s[i], sb);
            i++;
        }
    }

    // ── CSS ──────────────────────────────────────────────────────────────────

    private static void Css(string s, StringBuilder sb)
    {
        int depth = 0;
        int i = 0;
        while (i < s.Length)
        {
            char c = s[i];
            if (At(s, i, "/*"))
            {
                int end = s.IndexOf("*/", i + 2, StringComparison.Ordinal);
                i = Token(s, i, end < 0 ? s.Length : end + 2, "c", sb);
                continue;
            }
            if (c == '"' || c == '\'')
            {
                int close = s.IndexOf(c, i + 1);
                i = Token(s, i, close < 0 ? s.Length : close + 1, "s", sb);
                continue;
            }
            if (c == '{') depth++;
            if (c == '}') depth = Math.Max(0, depth - 1);
            if (c == '@')
            {
                int end = i + 1;
                while (end < s.Length && (char.IsLetter(s[end]) || s[end] == '-')) end++;
                i = Token(s, i, end, "k", sb);
                continue;
            }
            if (depth > 0 && (char.IsLetter(c) || c == '-'))
            {
                int end = i + 1;
                while (end < s.Length && (char.IsLetterOrDigit(s[end]) || s[end] == '-')) end++;
                // A name followed by ':' inside a rule is a property; anything else a value.
                i = NextNonSpace(s, end) == ':' ? Token(s, i, end, "p", sb) : Plain(s, i, end, sb);
                continue;
            }
            if (depth > 0 && (char.IsDigit(c) || c == '#' || c == '.' && i + 1 < s.Length && char.IsDigit(s[i + 1])))
            {
                int end = i + 1;
                while (end < s.Length && (char.IsLetterOrDigit(s[end]) || s[end] == '.' || s[end] == '%')) end++;
                i = Token(s, i, end, "n", sb);
                continue;
            }
            if (depth == 0 && !char.IsWhiteSpace(c) && c != '}' && c != ',')
            {
                int end = i;
                while (end < s.Length && s[end] != '{' && s[end] != ',' && s[end] != '\n' && !At(s, end, "/*")) end++;
                i = Token(s, i, end, "t", sb);
                continue;
            }
            Encode(c, sb);
            i++;
        }
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static bool At(string s, int i, string token) =>
        string.CompareOrdinal(s, i, token, 0, token.Length) == 0;

    private static char NextNonSpace(string s, int i)
    {
        while (i < s.Length && (s[i] == ' ' || s[i] == '\t')) i++;
        return i < s.Length ? s[i] : '\0';
    }

    private static int Token(string s, int start, int end, string kind, StringBuilder sb)
    {
        sb.Append("<span class=\"tk-").Append(kind).Append("\">");
        Encode(s.AsSpan(start, end - start), sb);
        sb.Append("</span>");
        return end;
    }

    private static int Plain(string s, int start, int end, StringBuilder sb)
    {
        Encode(s.AsSpan(start, end - start), sb);
        return end;
    }

    private static void Encode(ReadOnlySpan<char> text, StringBuilder sb)
    {
        foreach (char c in text) Encode(c, sb);
    }

    private static void Encode(char c, StringBuilder sb)
    {
        switch (c)
        {
            case '&': sb.Append("&amp;"); break;
            case '<': sb.Append("&lt;"); break;
            case '>': sb.Append("&gt;"); break;
            case '"': sb.Append("&quot;"); break;
            default: sb.Append(c); break;
        }
    }
}
