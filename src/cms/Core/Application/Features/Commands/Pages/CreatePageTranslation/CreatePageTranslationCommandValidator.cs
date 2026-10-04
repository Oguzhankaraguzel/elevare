using FluentValidation;

namespace Application.Features.Commands.Pages.CreatePageTranslation;

internal sealed class CreatePageTranslationCommandValidator : AbstractValidator<CreatePageTranslationCommand>
{
    public CreatePageTranslationCommandValidator()
    {
        RuleFor(x => x.SourcePageId).GreaterThan(0);
        RuleFor(x => x.TargetLanguageId).GreaterThan(0);
    }
}
