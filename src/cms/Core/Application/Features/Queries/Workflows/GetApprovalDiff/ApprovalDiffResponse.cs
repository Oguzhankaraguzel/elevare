using Domain.Entities.PageInfos;

namespace Application.Features.Queries.Workflows.GetApprovalDiff;

/// <summary>
/// The before/after picture for a pending approval.
/// </summary>
/// <param name="ContentTitle">Page title or template name, for the modal heading.</param>
/// <param name="HasStagedContent">
/// False when nothing was staged — the request is only asking for a status change,
/// or the content was published before the workflow was switched on.
/// </param>
/// <param name="IsFirstPublish">
/// True when there is no live version to compare against, so every line is new.
/// Worth saying out loud: an approver seeing a wall of green should know why.
/// </param>
/// <param name="CurrentStatus">What the public site shows today.</param>
/// <param name="RequestedStatus">What approving would switch it to, when that also changed.</param>
public sealed record ApprovalDiffResponse(
    string ContentTitle,
    bool HasStagedContent,
    bool IsFirstPublish,
    PageStatus? CurrentStatus,
    PageStatus? RequestedStatus,
    int AddedCount,
    int RemovedCount,
    IReadOnlyList<ApprovalDiffLine> Lines);
