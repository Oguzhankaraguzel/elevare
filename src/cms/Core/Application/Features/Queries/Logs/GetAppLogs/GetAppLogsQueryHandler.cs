using Application.Abstraction.Data;
using Domain.Entities.Logs;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Abstraction.Messaging;
using SharedKernel.Concrete;

namespace Application.Features.Queries.Logs.GetAppLogs;

internal sealed class GetAppLogsQueryHandler(ICmsApplicationDbContext db)
    : IQueryHandler<GetAppLogsQuery, PagedResult<AppLogResponse>>
{
    public async Task<Result<PagedResult<AppLogResponse>>> Handle(
        GetAppLogsQuery request, CancellationToken cancellationToken)
    {
        IQueryable<AppLog> query = db.AppLogs.AsNoTracking();

        if (request.Level is not null)
            query = query.Where(l => l.Level == request.Level);

        if (request.Source is not null)
            query = query.Where(l => l.Source == request.Source);

        if (request.FromUtc is not null)
            query = query.Where(l => l.CreatedAtUtc >= request.FromUtc);

        if (request.ToUtc is not null)
            query = query.Where(l => l.CreatedAtUtc <= request.ToUtc);

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            // Lower-cased both sides: LIKE is case-sensitive in PostgreSQL, so searching
            // "error" used to miss every message that said "Error". Suppressions and
            // full reasoning: SqlSearchProvider.
#pragma warning disable CA1304, CA1308, CA1311, CA1862
            string term = request.Search.Trim().ToLowerInvariant();
            query = query.Where(l =>
                l.Message.ToLower().Contains(term) ||
                l.Path != null && l.Path.ToLower().Contains(term) ||
                l.Exception != null && l.Exception.ToLower().Contains(term));
#pragma warning restore CA1304, CA1308, CA1311, CA1862
        }

        // CreatedAtUtc alone ties constantly at this row rate, so Id breaks the tie
        // — and matters more here than usual, because it is also the second half of
        // the (CreatedAtUtc, Id) tuple that IX_AppLogs_CreatedAtUtc_Id (see the
        // AddSearchAndPaginationIndexes migration) is built to serve.
        query = query.OrderByDescending(l => l.CreatedAtUtc).ThenByDescending(l => l.Id);

        int totalCount = await query.CountAsync(cancellationToken);

        // Deferred join: Skip/Take walks only the narrow (CreatedAtUtc, Id) index to
        // work out which rows belong on this page — an index-only scan — instead of
        // fetching and discarding the full (wide, Message/Exception-carrying) row for
        // every entry skipped over, which is what a plain `query.Skip(...)` above
        // would do. The full rows are then fetched by Id, a second, tiny query.
        List<long> pageIds = await query
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(l => l.Id)
            .ToListAsync(cancellationToken);

        List<AppLogResponse> items = await db.AppLogs.AsNoTracking()
            .Where(l => pageIds.Contains(l.Id))
            .OrderByDescending(l => l.CreatedAtUtc).ThenByDescending(l => l.Id)
            .Select(l => new AppLogResponse(
                l.Id, l.Level, l.Message, l.Exception, l.Source, l.Path, l.UserAgent, l.CreatedAtUtc))
            .ToListAsync(cancellationToken);

        return Result.Success(PagedResult<AppLogResponse>.Create(items, totalCount, request.Page, request.PageSize));
    }
}
