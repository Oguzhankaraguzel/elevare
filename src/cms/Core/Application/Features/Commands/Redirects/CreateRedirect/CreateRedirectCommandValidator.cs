using FluentValidation;

namespace Application.Features.Commands.Redirects.CreateRedirect;

internal sealed class CreateRedirectCommandValidator : AbstractValidator<CreateRedirectCommand>
{
    public CreateRedirectCommandValidator()
    {
        RuleFor(x => x.OldPath).NotEmpty().MaximumLength(1000);
        RuleFor(x => x.NewPath).MaximumLength(2000);
    }
}
