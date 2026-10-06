using System.Reflection;
using Application.Features.Queries.SiteSettings.GetSiteIdentity;
using MediatR;
using SharedKernel.Concrete;

namespace Wasm.Components.Services;

/// <summary>
/// The site this CMS manages, loaded once per circuit (or per request on the
/// static sign-in pages) and shared by the sidebar header, the browser-tab title
/// and the sign-in screen, so they never disagree and the settings are read once.
/// </summary>
public sealed class SiteIdentityState(ISender sender)
{
    /// <summary>"Elevare 1.0.0" — the build's own version, without the commit suffix.</summary>
    public static string ProductVersion { get; } = ReadProductVersion();

    private Task<SiteIdentityResponse?>? _load;

#pragma warning disable CA1003  // Using Func<Task> for Blazor StateHasChanged compatibility
    public event Func<Task>? OnChange;
#pragma warning restore CA1003

    public Task<SiteIdentityResponse?> GetAsync() => _load ??= LoadAsync();

    /// <summary>
    /// Re-reads the settings and tells every subscriber — called after Site
    /// Settings saves, so the header follows a renamed site without a reload.
    /// </summary>
    public async Task RefreshAsync()
    {
        _load = LoadAsync();
        await _load;
        if (OnChange is not null)
            await OnChange.Invoke();
    }

    private async Task<SiteIdentityResponse?> LoadAsync()
    {
        Result<SiteIdentityResponse> result = await sender.Send(new GetSiteIdentityQuery());
        return result.IsSuccess ? result.Value : null;
    }

    private static string ReadProductVersion()
    {
        string? version = typeof(SiteIdentityState).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        int metadata = version?.IndexOf('+', StringComparison.Ordinal) ?? -1;
        return metadata >= 0 ? version![..metadata] : version ?? string.Empty;
    }
}
