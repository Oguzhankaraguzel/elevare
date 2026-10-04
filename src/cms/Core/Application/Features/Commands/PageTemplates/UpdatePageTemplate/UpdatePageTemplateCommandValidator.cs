using FluentValidation;

namespace Application.Features.Commands.PageTemplates.UpdatePageTemplate;

internal sealed class UpdatePageTemplateCommandValidator : AbstractValidator<UpdatePageTemplateCommand>
{
    public UpdatePageTemplateCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Type).IsInEnum();
    }
}
