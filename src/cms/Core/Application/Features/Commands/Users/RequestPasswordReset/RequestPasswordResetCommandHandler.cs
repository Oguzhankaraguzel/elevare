using Application.Abstraction.Data;
using Application.Abstraction.Services.Email;
using Domain.Entities.Users;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using SharedKernel.Abstraction.Messaging;
using SharedKernel.Concrete;

namespace Application.Features.Commands.Users.RequestPasswordReset;

internal sealed class RequestPasswordResetCommandHandler(
    UserManager<AppUser> userManager,
    ICmsApplicationDbContext db,
    IEmailService emailService,
    ILogger<RequestPasswordResetCommandHandler> logger) : ICommandHandler<RequestPasswordResetCommand>
{
    public async Task<Result> Handle(RequestPasswordResetCommand request, CancellationToken cancellationToken)
    {
        string identifier = request.Identifier.Trim();
        if (identifier.Length == 0 || !await emailService.IsConfiguredAsync(cancellationToken))
            return Result.Success();

        // The same lookup as signing in, so whatever name works there works here.
        AppUser? user = identifier.Contains('@', StringComparison.Ordinal)
            ? await userManager.FindByEmailAsync(identifier)
            : await userManager.FindByNameAsync(identifier);
        user ??= await userManager.FindByNameAsync(identifier);

        if (user is null || !user.IsActive || string.IsNullOrWhiteSpace(user.Email))
            return Result.Success();

        (string token, _) = await PasswordLinks.IssueAsync(db, user.Id, PasswordLinks.SelfService, cancellationToken);
        await PasswordLinks.SendAsync(
            emailService, logger, user, PasswordLinks.BuildUrl(request.CmsBaseUrl, token),
            PasswordLinks.SelfService, PasswordLinkKind.Reset, cancellationToken);

        return Result.Success();
    }
}
