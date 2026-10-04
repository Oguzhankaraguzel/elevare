using Domain.Entities.Workflows;

namespace Application.Features.Queries.Dashboard.GetDashboardOverview;

/// <summary>One row in either "approvals I must decide" or "my content awaiting approval".</summary>
public sealed record WorkflowQueueItem(
    int ApprovalRequestId,
    WorkflowContentType ContentType,
    int ContentId,
    string ContentTitle,
    string WorkflowName,
    int CurrentStepOrder,
    int TotalSteps,
    string CurrentStepRoleName);
