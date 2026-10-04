using Application.Abstraction.Data;
using Application.Abstraction.Services.Authentication;
using Application.Abstraction.Services.Email;
using Domain.Entities.FormSubmissions;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Abstraction.Messaging;
using SharedKernel.Concrete;

namespace Application.Features.Commands.FormReplies.ReplyToFormSubmission;

internal sealed class ReplyToFormSubmissionCommandHandler(
    ICmsApplicationDbContext db,
    IEmailService emailService,
    IUserContext userContext) : ICommandHandler<ReplyToFormSubmissionCommand>
{
    public async Task<Result> Handle(ReplyToFormSubmissionCommand request, CancellationToken cancellationToken)
    {
        FormSubmission? submission = await db.FormSubmissions
            .FirstOrDefaultAsync(s => s.Id == request.SubmissionId, cancellationToken);

        if (submission is null)
            return Result.Failure(FormSubmissionErrors.NotFound);

        // Send first, stamp second. The reverse order would mark a submission
        // answered on a mail server that refused it, and nothing downstream would
        // ever show that the visitor is still waiting.
        Result sent = await emailService.SendAsync(
            EmailMessage.Create(request.ToEmail.Trim(), request.Subject.Trim(), request.Body),
            cancellationToken);

        if (sent.IsFailure)
            return sent;

        submission.RepliedAtUtc = DateTime.UtcNow;
        submission.RepliedByUserId = userContext.UserId;
        submission.ReplyToEmail = request.ToEmail.Trim();
        submission.ReplySubject = request.Subject.Trim();
        submission.ReplyBody = request.Body;

        return Result.Success();
    }
}
