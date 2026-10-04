using FluentValidation;

namespace Application.Features.Commands.Users.ChangeMyPassword;

internal sealed class ChangeMyPasswordCommandValidator : AbstractValidator<ChangeMyPasswordCommand>
{
    public ChangeMyPasswordCommandValidator()
    {
        RuleFor(x => x.CurrentPassword).NotEmpty();

        // Only the length floor lives here. Identity owns the rest of the policy
        // (digits, casing, symbols) and stating it twice guarantees the two drift
        // apart the first time the policy is tuned.
        RuleFor(x => x.NewPassword).NotEmpty().MinimumLength(8);
        RuleFor(x => x.NewPassword).NotEqual(x => x.CurrentPassword);
    }
}
