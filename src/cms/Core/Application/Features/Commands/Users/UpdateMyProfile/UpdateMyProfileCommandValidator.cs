using FluentValidation;

namespace Application.Features.Commands.Users.UpdateMyProfile;

internal sealed class UpdateMyProfileCommandValidator : AbstractValidator<UpdateMyProfileCommand>
{
    public UpdateMyProfileCommandValidator()
    {
        // Lengths mirror AppUser's MaxLength attributes, so a too-long value is
        // refused with a message instead of a truncation error from SQL Server.
        RuleFor(x => x.FirstName).MaximumLength(100);
        RuleFor(x => x.LastName).MaximumLength(100);
        RuleFor(x => x.Avatar).MaximumLength(500);
        RuleFor(x => x.Bio).MaximumLength(500);
        RuleFor(x => x.PhoneNumber).MaximumLength(30);
    }
}
