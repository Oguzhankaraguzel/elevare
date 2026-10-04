using FluentValidation;

namespace Application.Features.Commands.Redirects.UpdateRedirect;

internal sealed class UpdateRedirectCommandValidator : AbstractValidator<UpdateRedirectCommand>
{
    public UpdateRedirectCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.OldPath).NotEmpty().MaximumLength(1000);
        RuleFor(x => x.NewPath).MaximumLength(2000);
    }
}
