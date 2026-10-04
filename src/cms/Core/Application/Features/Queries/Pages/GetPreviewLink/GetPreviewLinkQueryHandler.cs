using Application.Abstraction.Data;
using Application.Abstraction.Services;
using Domain.Entities.PageInfos;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Abstraction.Messaging;
using SharedKernel.Concrete;

namespace Application.Features.Queries.Pages.GetPreviewLink;

internal sealed class GetPreviewLinkQueryHandler(ICmsApplicationDbContext db, IPreviewLinkSigner signer)
    : IQueryHandler<GetPreviewLinkQuery, string>
{
    private const string PublicSiteBaseUrlKey = "Advanced.PublicSiteBaseUrl";
    private static readonly TimeSpan LinkLifetime = TimeSpan.FromMinutes(15);

    public async Task<Result<string>> Handle(GetPreviewLinkQuery request, CancellationToken cancellationToken)
    {
        bool pageExists = await db.PageInfos
            .AsNoTracking()
            .AnyAsync(p => p.Id == request.PageId, cancellationToken);

        if (!pageExists)
            return Result.Failure<string>(PageInfoErrors.NotFound);

        string? baseUrl = await db.SiteSettings
            .AsNoTracking()
            .Where(s => s.Key == PublicSiteBaseUrlKey)
            .Select(s => s.Value)
            .FirstOrDefaultAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(baseUrl))
            return Result.Failure<string>(PageInfoErrors.PreviewBaseUrlNotConfigured);

        DateTimeOffset expiry = DateTimeOffset.UtcNow.Add(LinkLifetime);
        Result<string> token = signer.Sign(request.PageId, expiry);
        if (token.IsFailure)
            return Result.Failure<string>(token.Error);

        return Result.Success($"{baseUrl.TrimEnd('/')}/elevare-preview?token={token.Value}");
    }
}
