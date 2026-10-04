using Application.Abstraction.Data;
using Domain.Entities.SiteSettings;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Abstraction.Messaging;
using SharedKernel.Concrete;

namespace Application.Features.Queries.Seo.GetSeoSettings;

internal sealed class GetSeoSettingsQueryHandler(ICmsApplicationDbContext db)
    : IQueryHandler<GetSeoSettingsQuery, SeoSettingsResponse>
{
    public async Task<Result<SeoSettingsResponse>> Handle(
        GetSeoSettingsQuery request, CancellationToken cancellationToken)
    {
        Dictionary<string, string?> values = await db.SiteSettings
            .Where(s => !s.IsDeleted && SeoSettingKeys.All.Contains(s.Key))
            .ToDictionaryAsync(s => s.Key, s => s.Value, cancellationToken);

        return Result.Success(new SeoSettingsResponse(
            values.GetValueOrDefault(SeoSettingKeys.MetaTitleSuffix),
            values.GetValueOrDefault(SeoSettingKeys.MetaDescription),
            values.GetValueOrDefault(SeoSettingKeys.OgImage),
            values.GetValueOrDefault(SeoSettingKeys.RobotsTxt),
            values.GetValueOrDefault(SeoSettingKeys.LlmsTxt)));
    }
}
