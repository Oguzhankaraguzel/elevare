using Application.Abstraction.Data;
using SharedKernel.Abstraction.Messaging;
using Domain.Entities.SiteSettings;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Concrete;

namespace Application.Features.Queries.SiteSettings.GetSiteSettings;

public sealed record GetSiteSettingsQuery(SiteSettingGroup? Group = null) : IQuery<List<SiteSettingResponse>>;
