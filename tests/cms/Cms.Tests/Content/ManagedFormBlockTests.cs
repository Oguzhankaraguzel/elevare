using Shouldly;

namespace Cms.Tests.Content;

/// <summary>
/// Every page-builder block that collects something from a visitor must be a managed
/// form.
/// <para>
/// <c>data-elevare-managed-form</c> is the only thing that makes
/// <c>elevare-interactions.js</c> intercept the submit and POST it to
/// <c>/api/forms/submit</c>. Without it the browser falls back to its default: a GET
/// to the same URL. "Contact Form" and "Newsletter" both shipped without the
/// attribute, so a visitor's name, email and message went into the query string —
/// visible in the address bar, the browser history, the server access log and the
/// referrer of every outbound link — and nothing was stored anywhere. Nobody
/// noticed, because a form that appears to submit and clears itself looks like it
/// worked.
/// </para>
/// <para>
/// Asserted against the plugin source rather than a running editor, so the check is
/// free and runs in CI.
/// </para>
/// </summary>
public sealed class ManagedFormBlockTests
{
    /// <summary>
    /// Blocks whose form exists to capture visitor input. The search blocks are
    /// deliberately absent: their forms are navigation, not data collection, and
    /// marking them managed would break search — the plugin says as much where it
    /// decides which components get the managed-form traits.
    /// </summary>
    [Theory]
    [InlineData("elevare-contact")]
    [InlineData("elevare-newsletter")]
    [InlineData("elevare-multistep-form")]
    public void A_block_that_collects_visitor_input_marks_its_form_as_managed(string blockId)
    {
        string source = ReadBlocksPlugin();
        string block = ExtractBlock(source, blockId);

        block.ShouldContain("<form", Case.Sensitive,
            $"{blockId} was expected to contain a form");
        block.ShouldContain("data-elevare-managed-form", Case.Sensitive,
            $"{blockId} must mark its form as managed, or every submission is lost to the query string");
    }

    /// <summary>
    /// Reads from the block's id to the start of the next block, which is the whole
    /// of its definition including the template literal.
    /// </summary>
    private static string ExtractBlock(string source, string blockId)
    {
        int start = source.IndexOf($"id: '{blockId}'", StringComparison.Ordinal);
        start.ShouldBeGreaterThan(-1, $"block '{blockId}' should exist in elevare-blocks.js");

        int next = source.IndexOf("id: 'elevare-", start + 10, StringComparison.Ordinal);
        return next < 0 ? source[start..] : source[start..next];
    }

    private static string ReadBlocksPlugin()
    {
        string path = Path.Combine(
            FindRepositoryRoot(),
            "src", "cms", "Presentation", "Wasm", "Wasm", "wwwroot", "js", "grapesjs",
            "custom-plugins", "elevare-blocks.js");

        File.Exists(path).ShouldBeTrue("elevare-blocks.js should exist");
        return File.ReadAllText(path);
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
