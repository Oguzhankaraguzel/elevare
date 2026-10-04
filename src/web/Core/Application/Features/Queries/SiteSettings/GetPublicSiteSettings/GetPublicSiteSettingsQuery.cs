using SharedKernel.Abstraction.Messaging;

namespace Application.Features.Queries.SiteSettings.GetPublicSiteSettings;

/// <summary>Returns every site setting as a Key→Value map, for the shared layout to read from.</summary>
public sealed record GetPublicSiteSettingsQuery : IQuery<Dictionary<string, string?>>;
