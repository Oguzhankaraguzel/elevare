using Shouldly;

namespace Cms.Tests.Content;

/// <summary>
/// No editor may persist a canvas it could not read.
/// <para>
/// <c>grapesEditor.getData</c> returns null when the JS interop call fails — a
/// dropped Blazor circuit, a timeout — or when the editor never finished
/// initialising. All three editors used to log that and carry on, handing the update
/// command <c>gjs?.Html</c>, <c>gjs?.Css</c>, <c>gjs?.Data</c>: three nulls, which is
/// exactly what got written. The page's content was replaced with nothing while the
/// user was shown the normal "saved" toast, so the loss was silent and, once the
/// editor reloaded on empty content, unrecoverable from the UI. It was reproduced
/// twice against a real database before being fixed.
/// </para>
/// <para>
/// Asserted against the source text rather than a rendered component: these are
/// Razor pages whose save path needs a browser, a circuit and a live GrapesJS to
/// exercise, and the invariant worth protecting — "a null result must stop the save"
/// — is visible in the source without any of that.
/// </para>
/// </summary>
public sealed class EditorContentSaveTests
{
    /// <summary>
    /// Every component that reads the canvas through interop. Each one owns content
    /// that is destroyed if it saves null: pages, and the templates that pages embed.
    /// </summary>
    [Theory]
    [InlineData("Pages", "PageEditor.razor")]
    [InlineData("PageTemplates", "TemplateEditor.razor")]
    public void An_editor_refuses_to_save_a_canvas_it_could_not_read(string folder, string fileName)
    {
        string source = ReadEditor(folder, fileName);

        source.ShouldContain("grapesEditor.getData", Case.Sensitive,
            $"{fileName} was expected to read the canvas through interop");

        source.ShouldContain("if (gjs is null)", Case.Sensitive,
            $"{fileName} must stop when the canvas could not be read — without the guard it " +
            "saves null over the stored content and reports success");
    }

    /// <summary>
    /// The guard above is only worth anything if nothing downstream still reaches
    /// through a possibly-null result. <c>gjs?.Html</c> is the exact shape of the
    /// original bug: it compiles, it looks defensive, and it quietly sends null.
    /// After the guard the result is known non-null, so the null-conditional access
    /// has no reason to exist — its presence means a path skipped the guard.
    /// </summary>
    [Theory]
    [InlineData("Pages", "PageEditor.razor")]
    [InlineData("PageTemplates", "TemplateEditor.razor")]
    public void An_editor_never_reaches_through_a_possibly_null_canvas_result(string folder, string fileName)
    {
        string source = ReadEditor(folder, fileName);

        source.ShouldNotContain("gjs?.", Case.Sensitive,
            $"{fileName} still reads the canvas result with ?., which sends null on to the " +
            "server instead of stopping — guard on 'gjs is null' and then use gjs directly");
    }

    /// <summary>
    /// The circuit has to be able to carry a whole page back to the server.
    /// <para>
    /// A save returns the exported HTML, the exported CSS and the GrapesJS project
    /// JSON as one interop result, travelling browser-to-server — so it is bounded by
    /// the hub's <c>MaximumReceiveMessageSize</c>, which defaults to 32 KB. An
    /// ordinary rich page (hero, slider, gallery, video, form, footer) measured 58 KB
    /// and the hub simply closed the connection: the Save button span forever, no
    /// error reached the UI, and the only clue was a dropped circuit in the browser
    /// console. Left at the default, the page builder cannot save the very pages it
    /// exists to build.
    /// </para>
    /// </summary>
    [Fact]
    public void The_circuit_accepts_a_message_large_enough_to_carry_a_real_page()
    {
        string path = Path.Combine(FindRepositoryRoot(),
            "src", "cms", "Presentation", "Wasm", "Wasm", "Program.cs");

        File.Exists(path).ShouldBeTrue($"Program.cs should exist at {path}");
        string source = File.ReadAllText(path);

        source.ShouldContain("MaximumReceiveMessageSize", Case.Sensitive,
            "the Blazor hub must raise its 32 KB default, or saving any page bigger than " +
            "that silently drops the circuit instead of reporting anything");
    }

    private static string ReadEditor(string folder, string fileName)
    {
        string path = Path.Combine(
            FindRepositoryRoot(),
            "src", "cms", "Presentation", "Wasm", "Wasm", "Components", "Pages", folder, fileName);

        File.Exists(path).ShouldBeTrue($"{fileName} should exist at {path}");
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
