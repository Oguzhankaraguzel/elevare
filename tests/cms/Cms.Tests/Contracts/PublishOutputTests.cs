using System.Xml.Linq;
using Shouldly;

namespace Cms.Tests.Contracts;

/// <summary>
/// What a folder publish is allowed to contain.
/// <para>
/// A publish folder is copied wholesale onto a web server, so anything in it is on
/// that server. <c>appsettings.Development.json</c> holds the developer's connection
/// string, signing keys and first-run administrator password — and it shipped, until
/// the projects were told not to. If <c>ASPNETCORE_ENVIRONMENT</c> is ever
/// Development on that machine, those values are not merely present but live.
/// </para>
/// <para>
/// Asserted against the project files rather than a real publish, so the check costs
/// nothing and runs in CI without a publish step.
/// </para>
/// </summary>
public sealed class PublishOutputTests
{
    private static readonly string[] HostProjects =
    [
        Path.Combine("src", "cms", "Presentation", "Wasm", "Wasm", "Wasm.csproj"),
        Path.Combine("src", "web", "Presentation", "WebMvc", "WebMvc.csproj"),
    ];

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void A_host_project_never_publishes_its_development_settings(int index)
    {
        string path = Path.Combine(FindRepositoryRoot(), HostProjects[index]);
        File.Exists(path).ShouldBeTrue($"{HostProjects[index]} should exist");

        var project = XDocument.Load(path);

        bool excluded = project.Descendants("Content").Any(c =>
            (string?)c.Attribute("Update") == "appsettings.Development.json"
            && (string?)c.Attribute("CopyToPublishDirectory") == "Never");

        excluded.ShouldBeTrue(
            $"{HostProjects[index]} must mark appsettings.Development.json as CopyToPublishDirectory=\"Never\", "
            + "or a folder publish carries local secrets onto the server");
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
