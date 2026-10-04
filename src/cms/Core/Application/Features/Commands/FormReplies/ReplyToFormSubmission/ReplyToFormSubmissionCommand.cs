using Application.Abstraction.Security;
using Domain.Entities.Permissions;
using SharedKernel.Abstraction.Messaging;

namespace Application.Features.Commands.FormReplies.ReplyToFormSubmission;

/// <summary>
/// Sends an answer to a form submission and records what was sent.
/// <para>
/// Subject and body arrive already composed rather than as a template id: the
/// operator is allowed to edit a canned answer before sending, so the template is a
/// starting point, not the message.
/// </para>
/// </summary>
public sealed record ReplyToFormSubmissionCommand(
    int SubmissionId,
    string ToEmail,
    string Subject,
    string Body) : ICommand, IRequirePermission
{
    public static string RequiredPermission => PermissionKeys.FormsManageActions;
}
