using Application.Abstraction.Security;
using Domain.Entities.Permissions;
using Domain.Entities.SiteCodeSnippets;
using SharedKernel.Abstraction.Messaging;

namespace Application.Features.Commands.SiteCodeSnippets.SaveSiteCodeSnippet;

/// <summary>
/// Creates a snippet, or updates the one identified by <paramref name="Id"/>.
/// </summary>
/// <param name="RawInput">
/// Exactly what the author typed or pasted. For a preset this may be a bare id or
/// the vendor's whole block — <c>SiteCodePresetFactory</c> works out which. For
/// <see cref="SiteCodePreset.Custom"/> it is the markup itself.
/// </param>
/// <param name="Placement">
/// Ignored for presets that dictate their own position (Tag Manager's two halves,
/// consent pinned ahead of everything it must be able to block).
/// </param>
public sealed record SaveSiteCodeSnippetCommand(
    int? Id,
    string Name,
    SiteCodePreset Preset,
    SiteCodeKind Kind,
    SiteCodePlacement Placement,
    string? RawInput,
    bool IsEnabled,
    string? Notes) : ICommand<SaveSiteCodeSnippetResult>, IRequirePermission
{
    public static string RequiredPermission => PermissionKeys.CustomCodeAuthor;
}
