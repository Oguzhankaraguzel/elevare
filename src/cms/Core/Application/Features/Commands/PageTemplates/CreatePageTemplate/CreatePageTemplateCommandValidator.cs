using FluentValidation;

namespace Application.Features.Commands.PageTemplates.CreatePageTemplate;

internal sealed class CreatePageTemplateCommandValidator : AbstractValidator<CreatePageTemplateCommand>
{
    public CreatePageTemplateCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Type).IsInEnum();
    }
}
