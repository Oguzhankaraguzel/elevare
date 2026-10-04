using Application.Abstraction.Data;
using Application.Abstraction.Services.Email;
using Application.Features.Commands.Users;
using SharedKernel.Abstraction.Messaging;
using Domain.Entities.Users;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using SharedKernel.Concrete;

namespace Application.Features.Commands.Users.ResendPasswordSetup;

internal sealed class ResendPasswordSetupCommandHandler(
    UserManager<AppUser> userManager,
    ICmsApplicationDbContext db,
    IEmailService emailService,
    ILogger<ResendPasswordSetupCommandHandler> logger) : ICommandHandler<ResendPasswordSetupCommand, bool>
{
    public async Task<Result<bool>> Handle(ResendPasswordSetupCommand request, CancellationToken cancellationToken)
    {
        AppUser? user = await userManager.FindByIdAsync(request.UserId.ToString());
        if (user is null)
            return Result.Failure<bool>(AppUserErrors.NotFound);

        if (await userManager.HasPasswordAsync(user))
            return Result.Failure<bool>(PasswordSetupErrors.AlreadyHasPassword);

        bool emailSent = await PasswordSetupLinkMailer.SendAsync(
            db, emailService, logger, user, request.CmsBaseUrl, cancellationToken);

        return Result.Success(emailSent);
    }
}
