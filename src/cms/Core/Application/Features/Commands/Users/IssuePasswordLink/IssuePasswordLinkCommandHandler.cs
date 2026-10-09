using Application.Abstraction.Data;
using Application.Abstraction.Services.Authentication;
using Application.Abstraction.Services.Email;
using Domain.Entities.Users;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using SharedKernel.Abstraction.Messaging;
using SharedKernel.Concrete;

namespace Application.Features.Commands.Users.IssuePasswordLink;

internal sealed class IssuePasswordLinkCommandHandler(
    UserManager<AppUser> userManager,
    ICmsApplicationDbContext db,
    IEmailService emailService,
    IUserContext userContext,
    ILogger<IssuePasswordLinkCommandHandler> logger) : ICommandHandler<IssuePasswordLinkCommand, PasswordLinkResult>
{
    public async Task<Result<PasswordLinkResult>> Handle(IssuePasswordLinkCommand request, CancellationToken cancellationToken)
    {
        AppUser? user = await userManager.FindByIdAsync(request.UserId.ToString());
        if (user is null)
            return Result.Failure<PasswordLinkResult>(AppUserErrors.NotFound);

        Result allowed = await PasswordResetGuard.CheckAsync(userManager, userContext, user);
        if (allowed.IsFailure)
            return Result.Failure<PasswordLinkResult>(allowed.Error);

        (string token, DateTime expires) = await PasswordLinks.IssueAsync(
            db, user.Id, PasswordLinks.IssuedByAdministrator, cancellationToken);
        string url = PasswordLinks.BuildUrl(request.CmsBaseUrl, token);

        PasswordLinkKind kind = await userManager.HasPasswordAsync(user) ? PasswordLinkKind.Reset : PasswordLinkKind.Setup;
        bool emailSent = request.SendByEmail && await PasswordLinks.SendAsync(
            emailService, logger, user, url, PasswordLinks.IssuedByAdministrator, kind, cancellationToken);

        return Result.Success(new PasswordLinkResult(emailSent, emailSent ? null : url, expires));
    }
}
