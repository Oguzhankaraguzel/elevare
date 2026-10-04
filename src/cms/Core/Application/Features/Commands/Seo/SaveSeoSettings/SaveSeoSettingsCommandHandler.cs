using Application.Abstraction.Data;
using Application.Abstraction.Services.Authentication;
using Application.Security;
using Domain.Entities.SiteSettings;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Abstraction.Messaging;
using SharedKernel.Concrete;

namespace Application.Features.Commands.Seo.SaveSeoSettings;

internal sealed class SaveSeoSettingsCommandHandler(ICmsApplicationDbContext db, IUserContext userContext)
    : ICommandHandler<SaveSeoSettingsCommand>
{
    /// <summary>An llms.txt is a table of contents. Past this, it is a copy of the site.</summary>
    private const int MaxLlmsLength = 128 * 1024;

    public async Task<Result> Handle(SaveSeoSettingsCommand request, CancellationToken cancellationToken)
    {
        RobotsTxtValidationResult robots = RobotsTxtValidator.Validate(request.RobotsTxt);
        if (!robots.IsValid)
            return Result.Failure(robots.Error!);

        if (request.LlmsTxt is { Length: > MaxLlmsLength })
            return Result.Failure(SeoErrors.LlmsTooLarge);

        Dictionary<string, string?> incoming = new()
        {
            [SeoSettingKeys.MetaTitleSuffix] = request.MetaTitleSuffix,
            [SeoSettingKeys.MetaDescription] = request.MetaDescription,
            [SeoSettingKeys.OgImage] = request.OgImage,
            [SeoSettingKeys.RobotsTxt] = request.RobotsTxt,
            [SeoSettingKeys.LlmsTxt] = request.LlmsTxt,
        };

        // Only the keys this command actually carries. A null means the caller is not
        // editing that field, which is what lets one tab save without the others'
        // values riding along and overwriting a concurrent edit with stale text.
        string[] touched = [.. incoming.Where(pair => pair.Value is not null).Select(pair => pair.Key)];

        if (touched.Length == 0)
            return Result.Success();

        List<SiteSetting> rows = await db.SiteSettings
            .Where(s => !s.IsDeleted && touched.Contains(s.Key))
            .ToListAsync(cancellationToken);

        foreach (SiteSetting row in rows)
        {
            // Blank means "unset" — stored as null so the public side's
            // IsNullOrWhiteSpace checks fall back to their defaults.
            string? value = incoming[row.Key];
            row.Value = string.IsNullOrWhiteSpace(value) ? null : value;
            row.UpdateDate = DateTime.UtcNow;
            row.UpdateUserId = userContext.UserId;
        }

        return Result.Success();
    }
}
