using Application.Features.Commands.Users;
using Application.Abstraction.Data;
using Application.Abstraction.Services.Email;
using SharedKernel.Abstraction.Messaging;
using Domain.Entities.Users;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using SharedKernel.Concrete;

namespace Application.Features.Commands.Users.CreateUser;

internal sealed class CreateUserCommandHandler(
    UserManager<AppUser> userManager,
    ICmsApplicationDbContext db,
    IEmailService emailService,
    ILogger<CreateUserCommandHandler> logger) : ICommandHandler<CreateUserCommand, CreateUserResult>
{
    public async Task<Result<CreateUserResult>> Handle(CreateUserCommand request, CancellationToken cancellationToken)
    {
        AppUser? existing = await userManager.FindByEmailAsync(request.Email);
        if (existing is not null)
            return Result.Failure<CreateUserResult>(AppUserErrors.EmailAlreadyInUse);

        AppUser user = new()
        {
            Id = Guid.NewGuid(),
            Email = request.Email,
            UserName = request.UserName,
            FirstName = request.FirstName,
            LastName = request.LastName,
            Bio = request.Bio,
            IsActive = request.IsActive,
            EmailConfirmed = true
        };

        // No password overload: the account is created with PasswordHash left null,
        // and the user sets their own via the link below, mailed or handed over. They
        // simply can't sign in until they do — there is no admin-visible plaintext
        // password to mishandle in the meantime.
        IdentityResult result = await userManager.CreateAsync(user);
        if (!result.Succeeded)
        {
            return Result.Failure<CreateUserResult>(AppUserErrors.CreateFailed(result.Describe()));
        }

        if (!string.IsNullOrWhiteSpace(request.Role))
            await userManager.AddToRoleAsync(user, request.Role);

        (string token, DateTime expires) = await PasswordLinks.IssueAsync(
            db, user.Id, PasswordLinks.IssuedByAdministrator, cancellationToken);
        string url = PasswordLinks.BuildUrl(request.CmsBaseUrl, token);

        bool emailSent = await PasswordLinks.SendAsync(
            emailService, logger, user, url, PasswordLinks.IssuedByAdministrator, PasswordLinkKind.Setup, cancellationToken);

        return Result.Success(new CreateUserResult(user.Id, emailSent, emailSent ? null : url, expires));
    }
}
