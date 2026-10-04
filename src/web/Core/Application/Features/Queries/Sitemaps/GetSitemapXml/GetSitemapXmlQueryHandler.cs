using Application.Abstraction.Data;
using Domain.Entities.PublicSitemaps;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Abstraction.Messaging;
using SharedKernel.Concrete;

namespace Application.Features.Queries.Sitemaps.GetSitemapXml;

internal sealed class GetSitemapXmlQueryHandler(IPublicReadDbContext db)
    : IQueryHandler<GetSitemapXmlQuery, string>
{
    public async Task<Result<string>> Handle(GetSitemapXmlQuery request, CancellationToken cancellationToken)
    {
        PublicSitemapCache? cache = await db.SitemapCaches
            .FirstOrDefaultAsync(s => s.CacheKey == request.CacheKey, cancellationToken);

        if (cache is null)
            return Result.Failure<string>(PublicSitemapErrors.NotFound(request.CacheKey));

        return Result.Success(cache.XmlContent);
    }
}
