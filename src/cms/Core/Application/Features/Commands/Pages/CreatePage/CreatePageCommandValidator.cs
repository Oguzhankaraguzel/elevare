using FluentValidation;

namespace Application.Features.Commands.Pages.CreatePage;

internal sealed class CreatePageCommandValidator : AbstractValidator<CreatePageCommand>
{
    public CreatePageCommandValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Slug).MaximumLength(50);
        RuleFor(x => x.LanguageId).GreaterThan(0);
        RuleFor(x => x.ParentPageId).GreaterThan(0).When(x => x.ParentPageId.HasValue);
    }
}
