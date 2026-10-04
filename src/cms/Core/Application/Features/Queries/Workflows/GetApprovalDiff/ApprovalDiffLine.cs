namespace Application.Features.Queries.Workflows.GetApprovalDiff;

/// <summary>One line of reader-visible text, and what happened to it.</summary>
public sealed record ApprovalDiffLine(ApprovalDiffKind Kind, string Text);
