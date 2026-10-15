using System.Globalization;
using System.Text.RegularExpressions;
using AngleSharp.Dom;

namespace Application.Services;

/// <summary>
/// Estimates how wide an image is drawn, from the page's own stylesheet, for its
/// <c>sizes</c> hint — so a card image in a three-column grid stops downloading the
/// file meant for a full-width hero.
/// </summary>
/// <remarks>
/// <para>
/// Reads only what the builder writes: top-level rules on a single class or id
/// (<c>.cards{grid-template-columns:repeat(3,1fr)}</c>, <c>#box{max-width:1200px}</c>).
/// Walking up from the image, each grid it sits in divides its width by the
/// number of columns, and the first pixel width (an element's <c>width</c> or
/// <c>max-width</c>, or a grid track like the <c>320px</c> of
/// <c>320px 1fr</c>) ends the walk. Media queries are not read: grids in the
/// builder's blocks fold to one column on phones, which the hint says separately.
/// </para>
/// <para>
/// It is an upper bound, not a measurement — gaps and padding are ignored, so the
/// browser may pick one size larger than it needs, never a blurry smaller one.
/// Where nothing can be said, it says nothing and the caller keeps its fallback.
/// </para>
/// </remarks>
public sealed class ImageLayoutEstimator
{
    /// <summary>Below this the builder's grids are one column (its blocks' own breakpoint).</summary>
    public const int SingleColumnBelowPx = 767;

    private static readonly Regex Comments = new(@"/\*.*?\*/", RegexOptions.Singleline | RegexOptions.Compiled);
    private static readonly Regex SimpleSelector = new(@"^[.#][A-Za-z_][\w-]*$", RegexOptions.Compiled);
    private static readonly Regex Pixels = new(@"^(?<px>\d{1,5}(?:\.\d+)?)px$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex Repeat = new(@"repeat\(\s*(?<n>\d{1,2})\s*,\s*(?<track>[^)]+)\)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex AutoRepeat = new(
        @"repeat\(\s*auto-(?:fit|fill)\s*,\s*minmax\(\s*(?:min\(\s*100%\s*,\s*)?(?<px>\d{1,5}(?:\.\d+)?)px",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private const int MaxDepth = 24;

    /// <summary>Declarations by selector (".name" / "#name"), in source order.</summary>
    private readonly Dictionary<string, List<Dictionary<string, string>>> _rules = new(StringComparer.Ordinal);

    public ImageLayoutEstimator(string? css)
    {
        if (!string.IsNullOrWhiteSpace(css))
            Parse(Comments.Replace(css, string.Empty));
    }

    /// <summary>A <c>sizes</c> value for <paramref name="image"/>, or null when the stylesheet says nothing useful.</summary>
    public string? EstimateSizes(IElement image)
    {
        ArgumentNullException.ThrowIfNull(image);
        if (_rules.Count == 0)
            return null;

        double fraction = 1;
        // An auto-fit grid's column count depends on the width it is given, so it
        // waits for the next pixel width up — see AutoFitMinimum.
        double? autoFitMinimum = null;
        IElement child = image;
        int depth = 0;
        for (IElement? element = image;
             element is not null && !IsRoot(element) && depth < MaxDepth;
             element = element.ParentElement, depth++)
        {
            Dictionary<string, string> declarations = DeclarationsFor(element);

            if (element != image && autoFitMinimum is null
                && declarations.TryGetValue("grid-template-columns", out string? columns))
            {
                if (AutoFitMinimum(columns) is double minimum)
                {
                    autoFitMinimum = minimum;
                }
                else if (Tracks(columns) is { Count: > 1 } tracks)
                {
                    string track = tracks[IndexAmongSiblings(child) % tracks.Count];
                    if (TryPixels(track, out double trackPx))
                        return Describe(fraction * trackPx, inGrid: true);
                    fraction /= tracks.Count;
                }
            }

            if (PixelWidth(declarations) is double px)
            {
                if (autoFitMinimum is double min)
                {
                    // As many columns of at least `min` as fit, sharing the width.
                    double columnCount = Math.Max(1, Math.Floor(px / min));
                    return Describe(fraction * px / columnCount, inGrid: true);
                }
                return Describe(fraction * px, inGrid: fraction < 1);
            }

            child = element;
        }

        // No pixel width above an auto-fit grid: a column is narrower than two
        // minimums, or a second one would have fitted beside it.
        if (autoFitMinimum is double unbounded)
            return Describe(fraction * unbounded * 2, inGrid: true);

        // No pixel width anywhere above: a share of the viewport, when a grid gave one.
        if (fraction >= 1)
            return null;
        string vw = Math.Max(1, (int)Math.Ceiling(fraction * 100)).ToString(CultureInfo.InvariantCulture);
        return $"(max-width: {SingleColumnBelowPx.ToString(CultureInfo.InvariantCulture)}px) 100vw, {vw}vw";
    }

    private static string Describe(double px, bool inGrid)
    {
        string width = Math.Max(1, (int)Math.Ceiling(px)).ToString(CultureInfo.InvariantCulture);
        // A grid cell is the full width once the grid folds on a phone; a capped
        // block is the full width until the screen is wider than the cap.
        string below = inGrid ? SingleColumnBelowPx.ToString(CultureInfo.InvariantCulture) : width;
        return $"(max-width: {below}px) 100vw, {width}px";
    }

    private Dictionary<string, string> DeclarationsFor(IElement element)
    {
        // Classes in source order, then the id, which outranks them.
        Dictionary<string, string> merged = new(StringComparer.OrdinalIgnoreCase);
        foreach (string name in element.ClassList)
            Apply(merged, "." + name);
        if (!string.IsNullOrEmpty(element.Id))
            Apply(merged, "#" + element.Id);
        return merged;
    }

    private void Apply(Dictionary<string, string> merged, string selector)
    {
        if (!_rules.TryGetValue(selector, out List<Dictionary<string, string>>? rules))
            return;
        foreach (Dictionary<string, string> rule in rules)
            foreach ((string property, string value) in rule)
                merged[property] = value;
    }

    private void Parse(string css)
    {
        int i = 0;
        while (i < css.Length)
        {
            int open = css.IndexOf('{', i);
            if (open < 0)
                return;
            string prelude = css[i..open].Trim();
            int close = MatchingBrace(css, open);
            if (close < 0)
                return;

            // At-rules (@media, @supports, @keyframes …) are skipped whole.
            if (!prelude.StartsWith('@'))
            {
                Dictionary<string, string> declarations = Declarations(css[(open + 1)..close]);
                if (declarations.Count > 0)
                {
                    foreach (string selector in prelude.Split(','))
                    {
                        string trimmed = selector.Trim();
                        if (!SimpleSelector.IsMatch(trimmed))
                            continue;
                        if (!_rules.TryGetValue(trimmed, out List<Dictionary<string, string>>? list))
                            _rules[trimmed] = list = [];
                        list.Add(declarations);
                    }
                }
            }
            i = close + 1;
        }
    }

    private static int MatchingBrace(string css, int open)
    {
        int depth = 0;
        for (int i = open; i < css.Length; i++)
        {
            if (css[i] == '{') depth++;
            else if (css[i] == '}' && --depth == 0) return i;
        }
        return -1;
    }

    private static Dictionary<string, string> Declarations(string body)
    {
        Dictionary<string, string> result = new(StringComparer.OrdinalIgnoreCase);
        foreach (string part in body.Split(';'))
        {
            int colon = part.IndexOf(':', StringComparison.Ordinal);
            if (colon <= 0)
                continue;
            string property = part[..colon].Trim();
            string value = part[(colon + 1)..].Replace("!important", string.Empty, StringComparison.OrdinalIgnoreCase).Trim();
            if (property is "width" or "max-width" or "grid-template-columns")
                result[property] = value;
        }
        return result;
    }

    /// <summary>
    /// The minimum column width of <c>repeat(auto-fit|auto-fill, minmax(320px, 1fr))</c>
    /// — also written <c>minmax(min(100%, 320px), 1fr)</c> so one column never
    /// overflows a phone — or null for any other value.
    /// </summary>
    private static double? AutoFitMinimum(string value)
    {
        Match match = AutoRepeat.Match(value);
        return match.Success && TryPixels(match.Groups["px"].Value + "px", out double px) ? px : null;
    }

    /// <summary>The column tracks of a <c>grid-template-columns</c> value, <c>repeat()</c> expanded.</summary>
    private static List<string> Tracks(string value)
    {
        string expanded = Repeat.Replace(value, m =>
        {
            int count = int.Parse(m.Groups["n"].Value, CultureInfo.InvariantCulture);
            return string.Join(' ', Enumerable.Repeat(m.Groups["track"].Value.Trim(), count));
        });

        // minmax(…) and other functions are one track each: split on spaces outside parentheses.
        List<string> tracks = [];
        int depth = 0, start = 0;
        for (int i = 0; i <= expanded.Length; i++)
        {
            char c = i < expanded.Length ? expanded[i] : ' ';
            if (c == '(') depth++;
            else if (c == ')') depth--;
            else if (char.IsWhiteSpace(c) && depth == 0)
            {
                if (i > start)
                    tracks.Add(expanded[start..i]);
                start = i + 1;
            }
        }
        return tracks;
    }

    /// <summary>The element's own <c>width</c> in pixels, or its <c>max-width</c>.</summary>
    private static double? PixelWidth(Dictionary<string, string> declarations)
    {
        foreach (string property in (string[])["width", "max-width"])
        {
            if (declarations.TryGetValue(property, out string? value) && TryPixels(value, out double px))
                return px;
        }
        return null;
    }

    private static bool TryPixels(string value, out double px)
    {
        Match match = Pixels.Match(value.Trim());
        px = match.Success ? double.Parse(match.Groups["px"].Value, CultureInfo.InvariantCulture) : 0;
        return match.Success && px > 0;
    }

    private static int IndexAmongSiblings(IElement element)
    {
        int index = 0;
        for (IElement? sibling = element.PreviousElementSibling; sibling is not null; sibling = sibling.PreviousElementSibling)
            index++;
        return index;
    }

    private static bool IsRoot(IElement element) =>
        element.LocalName is "body" or "html";
}
