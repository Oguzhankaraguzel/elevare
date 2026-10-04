using System.Text.RegularExpressions;
using Shouldly;

namespace Cms.Tests.Contracts;

/// <summary>
/// The page builder's brand-colour picker writes <c>var(--elevare-x, #fallback)</c>,
/// and that fallback is supposed to be the value the seeded palette gives the same
/// token — so a page whose palette variable has gone missing degrades to what a fresh
/// install looks like, not to some colour that never belonged to this product.
/// <para>
/// The list lives in <c>grapes-editor.js</c> because the picker has to name a colour
/// before any request could answer, so it is a copy of what
/// <c>DatabaseSeeder</c> seeds. A copy nobody checks drifts: the seeder's palette can
/// be edited without anyone remembering a JavaScript array exists. This is the check.
/// </para>
/// <para>
/// Compared against the seeder's <c>:root</c> block specifically. A <c>var()</c>
/// fallback is one static string and cannot follow <c>[data-theme="dark"]</c>, so the
/// base palette is the only one that can mean "default".
/// </para>
/// </summary>
public sealed class BrandTokenFallbackTests
{
    private static readonly string EditorScript =
        Path.Combine("src", "cms", "Presentation", "Wasm", "Wasm", "wwwroot", "js", "grapesjs", "grapes-editor.js");

    private static readonly string Seeder =
        Path.Combine("src", "cms", "Infrastructure", "Persistence", "Seed", "DatabaseSeeder.cs");

    [Fact]
    public void Every_picker_fallback_is_the_seeded_default_for_that_token()
    {
        Dictionary<string, string> picker = ReadPickerFallbacks();
        Dictionary<string, string> seeded = ReadSeededRootPalette();

        picker.ShouldNotBeEmpty("BRAND_TOKENS could not be parsed out of grapes-editor.js");

        foreach ((string token, string fallback) in picker)
        {
            seeded.ShouldContainKey(token,
                $"the picker offers {token}, but the seeded :root palette does not define it");
            fallback.ShouldBe(seeded[token],
                $"the picker's fallback for {token} has drifted from the seeded default");
        }
    }

    [Fact]
    public void The_picker_offers_every_token_the_palette_defines()
    {
        Dictionary<string, string> picker = ReadPickerFallbacks();
        Dictionary<string, string> seeded = ReadSeededRootPalette();

        // The other direction: a token added to the palette but never offered in the
        // picker is invisible to authors, who then reach for a hex instead.
        foreach (string token in seeded.Keys)
            picker.ShouldContainKey(token,
                $"the seeded palette defines {token}, but the colour picker never offers it");
    }

    private static Dictionary<string, string> ReadPickerFallbacks()
    {
        string source = File.ReadAllText(Path.Combine(FindRepositoryRoot(), EditorScript));

        int start = source.IndexOf("const BRAND_TOKENS = [", StringComparison.Ordinal);
        start.ShouldBeGreaterThan(-1, "BRAND_TOKENS is missing from grapes-editor.js");
        int end = source.IndexOf("];", start, StringComparison.Ordinal);
        end.ShouldBeGreaterThan(start, "the BRAND_TOKENS array is not terminated");

        return Regex
            .Matches(source[start..end], @"name:\s*'(--elevare-[\w-]+)'\s*,\s*fallback:\s*'(#[0-9a-fA-F]{3,8})'")
            .ToDictionary(m => m.Groups[1].Value, m => m.Groups[2].Value.ToUpperInvariant());
    }

    private static Dictionary<string, string> ReadSeededRootPalette()
    {
        string source = File.ReadAllText(Path.Combine(FindRepositoryRoot(), Seeder));

        // Only the :root block — the dark block redefines the same names, and taking
        // both would collapse them onto whichever came last.
        int start = source.IndexOf(":root {", StringComparison.Ordinal);
        start.ShouldBeGreaterThan(-1, "the seeded :root palette could not be located in DatabaseSeeder");
        int end = source.IndexOf('}', start);
        end.ShouldBeGreaterThan(start, "the seeded :root block is not terminated");

        return Regex
            .Matches(source[start..end], @"(--elevare-[\w-]+)\s*:\s*(#[0-9a-fA-F]{3,8})\s*;")
            .ToDictionary(m => m.Groups[1].Value, m => m.Groups[2].Value.ToUpperInvariant());
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Elevare.sln")))
            directory = directory.Parent;

        directory.ShouldNotBeNull("the repository root (the folder holding Elevare.sln) could not be located");
        return directory!.FullName;
    }
}
