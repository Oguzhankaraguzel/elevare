using Application.Abstraction.Data;
using SharedKernel.Abstraction.Messaging;
using Domain.Entities.SiteSettings;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Concrete;

namespace Application.Features.Queries.SiteSettings.GetSiteSettings;

internal sealed class GetSiteSettingsQueryHandler : IQueryHandler<GetSiteSettingsQuery, List<SiteSettingResponse>>
{
    private readonly ICmsApplicationDbContext _db;

    public GetSiteSettingsQueryHandler(ICmsApplicationDbContext db) => _db = db;

    public async Task<Result<List<SiteSettingResponse>>> Handle(GetSiteSettingsQuery request, CancellationToken cancellationToken)
    {
        IQueryable<SiteSetting> query = _db.SiteSettings.AsNoTracking().Where(s => !s.IsDeleted);
        if (request.Group.HasValue)
            query = query.Where(s => s.Group == request.Group.Value);

        List<SiteSettingResponse> items = await query
            .OrderBy(s => s.Group).ThenBy(s => s.Key)
            .Select(s => new SiteSettingResponse(s.Id, s.Key, s.Value, s.DisplayName, s.Description, s.Group, s.IsSystem, s.DataType))
            .ToListAsync(cancellationToken);

        return Result.Success(items);
    }
}
