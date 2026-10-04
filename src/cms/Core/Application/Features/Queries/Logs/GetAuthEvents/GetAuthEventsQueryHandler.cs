using Application.Abstraction.Data;
using Domain.Entities.Logs;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Abstraction.Messaging;
using SharedKernel.Concrete;

namespace Application.Features.Queries.Logs.GetAuthEvents;

internal sealed class GetAuthEventsQueryHandler(ICmsApplicationDbContext db)
    : IQueryHandler<GetAuthEventsQuery, PagedResult<AuthEventResponse>>
{
    public async Task<Result<PagedResult<AuthEventResponse>>> Handle(
        GetAuthEventsQuery request, CancellationToken cancellationToken)
    {
        IQueryable<AuthEvent> query = db.AuthEvents.AsNoTracking();

        if (request.EventType is not null)
            query = query.Where(e => e.EventType == request.EventType);

        if (request.FromUtc is not null)
            query = query.Where(e => e.CreatedAtUtc >= request.FromUtc);

        if (request.ToUtc is not null)
            query = query.Where(e => e.CreatedAtUtc <= request.ToUtc);

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            // Lower-cased both sides: LIKE is case-sensitive in PostgreSQL, so searching
            // "error" used to miss every message that said "Error". Suppressions and
            // full reasoning: SqlSearchProvider.
#pragma warning disable CA1304, CA1308, CA1311, CA1862
            string term = request.Search.Trim().ToLowerInvariant();
            query = query.Where(e =>
                e.UserNameSnapshot.ToLower().Contains(term) ||
                e.IpAddress != null && e.IpAddress.ToLower().Contains(term));
#pragma warning restore CA1304, CA1308, CA1311, CA1862
        }

        // Id breaks CreatedAtUtc ties and doubles as the second half of the
        // (CreatedAtUtc, Id) tuple IX_AuthEvents_CreatedAtUtc_Id (see the
        // AddSearchAndPaginationIndexes migration) is built to serve.
        query = query.OrderByDescending(e => e.CreatedAtUtc).ThenByDescending(e => e.Id);

        int totalCount = await query.CountAsync(cancellationToken);

        // Deferred join — see GetAppLogsQueryHandler for why: Skip/Take here only
        // walks the narrow (CreatedAtUtc, Id) index to find this page's row
        // identities (index-only scan), instead of fetching every skipped row's
        // full width just to discard it. The actual rows are a second, tiny query.
        List<long> pageIds = await query
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(e => e.Id)
            .ToListAsync(cancellationToken);

        List<AuthEventResponse> items = await db.AuthEvents.AsNoTracking()
            .Where(e => pageIds.Contains(e.Id))
            .OrderByDescending(e => e.CreatedAtUtc).ThenByDescending(e => e.Id)
            .Select(e => new AuthEventResponse(
                e.Id, e.EventType, e.UserId, e.UserNameSnapshot, e.IpAddress, e.UserAgent, e.CreatedAtUtc))
            .ToListAsync(cancellationToken);

        return Result.Success(PagedResult<AuthEventResponse>.Create(items, totalCount, request.Page, request.PageSize));
    }
}
