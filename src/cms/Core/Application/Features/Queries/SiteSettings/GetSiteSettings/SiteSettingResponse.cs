using Application.Abstraction.Data;
using SharedKernel.Abstraction.Messaging;
using Domain.Entities.SiteSettings;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Concrete;

namespace Application.Features.Queries.SiteSettings.GetSiteSettings;

public sealed record SiteSettingResponse(
    int Id, string Key, string? Value, string DisplayName,
    string? Description, SiteSettingGroup Group, bool IsSystem, string? DataType);
