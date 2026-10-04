using System.Text.RegularExpressions;
using Shouldly;

namespace Cms.Tests.Layers;

/// <summary>
/// Nothing may report a failure through <c>Console</c>.
/// <para>
/// Both applications configure Serilog as the logging provider, which writes to the
/// console sink *and* to Seq. A direct <c>Console.Error.WriteLine</c> bypasses all of
/// that: it is not structured, carries no category or correlation, and in a
/// container it lands nowhere anyone is looking. The CMS had 73 of them, every one
/// inside a <c>catch</c> — which is to say, every one of them was an error report
/// that silently went missing.
/// </para>
/// </summary>
public sealed class NoConsoleLoggingTests
{
    private static readonly Regex ConsoleWrite = new(
        @"\bConsole\s*\.\s*(Error\s*\.\s*)?Write", RegexOptions.Compiled);

    [Fact]
    public void The_scan_actually_reaches_the_source_tree()
    {
        // Without this, a wrong root or a changed layout turns the test below into an
        // assertion about an empty list, which passes and proves nothing.
        EnumerateSourceFiles(Path.Combine(FindRepositoryRoot(), "src"))
            .Count()
            .ShouldBeGreaterThan(200);
    }

    [Fact]
    public void No_source_file_writes_to_the_console()
    {
        string root = FindRepositoryRoot();
        List<string> offenders = [];

        foreach (string file in EnumerateSourceFiles(Path.Combine(root, "src")))
        {
            string text = File.ReadAllText(file);
            if (ConsoleWrite.IsMatch(text))
                offenders.Add(Path.GetRelativePath(root, file));
        }

        offenders.ShouldBeEmpty(
            "these files log through Console instead of ILogger, so their output never reaches Serilog: "
            + string.Join(", ", offenders));
    }

    private static IEnumerable<string> EnumerateSourceFiles(string directory) =>
        Directory.EnumerateFiles(directory, "*.*", SearchOption.AllDirectories)
            .Where(f => f.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)
                        || f.EndsWith(".razor", StringComparison.OrdinalIgnoreCase))
            // Build output contains generated copies of the sources; scanning them
            // would report the same file twice and, worse, keep reporting a violation
            // after it was fixed until someone cleaned the output folder.
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                        && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal));

    /// <summary>
    /// Walks up from the test assembly until the solution file appears. Hard-coding a
    /// relative hop count breaks the moment the test project moves or the build
    /// output layout changes.
    /// </summary>
    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Elevare.sln")))
            directory = directory.Parent;

        directory.ShouldNotBeNull("the repository root (the folder holding Elevare.sln) could not be located");
        return directory!.FullName;
    }
}
