using Application.Abstraction.Data;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Abstraction.Messaging;
using SharedKernel.Concrete;

namespace Application.Features.Queries.FormSubmissions.GetFormSubmissions;

internal sealed class GetFormSubmissionsQueryHandler(ICmsApplicationDbContext db)
    : IQueryHandler<GetFormSubmissionsQuery, PagedResult<FormSubmissionResponse>>
{
    public async Task<Result<PagedResult<FormSubmissionResponse>>> Handle(
        GetFormSubmissionsQuery request,
        CancellationToken cancellationToken)
    {
        IQueryable<Domain.Entities.FormSubmissions.FormSubmission> baseQuery = db.FormSubmissions.AsNoTracking()
            // Id tie-breaks SubmittedAtUtc, and completes the (SubmittedAtUtc, Id)
            // tuple IX_FormSubmissions_SubmittedAtUtc_Id (see the
            // AddSearchAndPaginationIndexes migration) is built to serve.
            .OrderByDescending(f => f.SubmittedAtUtc).ThenByDescending(f => f.Id);

        int totalCount = await baseQuery.CountAsync(cancellationToken);

        // Deferred join: find this page's row identities from the unindexed-column-free
        // base table first (an index-only scan against the tuple index above), THEN
        // join to PageInfo/RepliedByUser only for those rows — instead of paying for
        // the join on every row skipped over, which `Include(...).Skip(...)` would.
        List<int> pageIds = await baseQuery
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(f => f.Id)
            .ToListAsync(cancellationToken);

        IQueryable<Domain.Entities.FormSubmissions.FormSubmission> query = db.FormSubmissions
            .AsNoTracking()
            .Where(f => pageIds.Contains(f.Id))
            .Include(f => f.PageInfo)
            .Include(f => f.RepliedByUser)
            .OrderByDescending(f => f.SubmittedAtUtc).ThenByDescending(f => f.Id);

        var rows = await query
            .Select(f => new
            {
                f.Id,
                f.PageInfoId,
                PageTitle = f.PageInfo!.SeoMeta.Title,
                f.PageInfo.FullSlug,
                f.FormName,
                f.FieldsJson,
                f.SubmittedAtUtc,
                f.RepliedAtUtc,
                ReplierFullName = f.RepliedByUser == null ? null : (f.RepliedByUser.FirstName + " " + f.RepliedByUser.LastName).Trim(),
                ReplierUserName = f.RepliedByUser == null ? null : f.RepliedByUser.UserName,
                f.ReplyToEmail,
                f.ReplySubject,
                f.ReplyBody,
            })
            .ToListAsync(cancellationToken);

        // Attachments are listed by name and size only; the bytes stay in the
        // database until someone downloads one (FormAttachmentEndpoints).
        var attachmentsBySubmission = (await db.FormSubmissionAttachments
            .AsNoTracking()
            .Where(a => pageIds.Contains(a.FormSubmissionId))
            .OrderBy(a => a.Id)
            .Select(a => new { a.Id, a.FormSubmissionId, a.FieldName, a.FileName, a.Size })
            .ToListAsync(cancellationToken))
            .GroupBy(a => a.FormSubmissionId)
            .ToDictionary(g => g.Key, g => g.Select(a => new FormAttachmentSummary(a.Id, a.FieldName, a.FileName, a.Size)).ToList());

        List<FormSubmissionResponse> items = [.. rows.Select(r => new FormSubmissionResponse(
            r.Id,
            r.PageInfoId,
            r.PageTitle,
            r.FullSlug,
            r.FormName,
            r.FieldsJson,
            r.SubmittedAtUtc,
            r.RepliedAtUtc,
            string.IsNullOrWhiteSpace(r.ReplierFullName) ? r.ReplierUserName : r.ReplierFullName,
            r.ReplyToEmail,
            r.ReplySubject,
            r.ReplyBody,
            attachmentsBySubmission.GetValueOrDefault(r.Id) ?? []))];

        return Result.Success(PagedResult<FormSubmissionResponse>.Create(items, totalCount, request.Page, request.PageSize));
    }
}
