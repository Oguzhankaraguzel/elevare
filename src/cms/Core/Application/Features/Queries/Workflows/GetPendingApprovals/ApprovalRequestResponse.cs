using Domain.Entities.Workflows;

namespace Application.Features.Queries.Workflows.GetPendingApprovals;

public sealed record ApprovalRequestResponse(
    int Id,
    WorkflowContentType ContentType,
    int ContentId,
    string ContentTitle,
    string WorkflowName,
    int CurrentStepOrder,
    int TotalSteps,
    string CurrentStepRoleName,
    /// <summary>Set when the step belongs to one named person rather than the whole role.</summary>
    string? CurrentStepUserName,
    string RequestedByUserName,
    DateTime CreateDate,
    bool CanDecide);
