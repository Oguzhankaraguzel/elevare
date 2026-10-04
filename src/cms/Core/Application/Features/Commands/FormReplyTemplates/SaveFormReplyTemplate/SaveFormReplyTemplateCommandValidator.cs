using FluentValidation;

namespace Application.Features.Commands.FormReplyTemplates.SaveFormReplyTemplate;

internal sealed class SaveFormReplyTemplateCommandValidator : AbstractValidator<SaveFormReplyTemplateCommand>
{
    public SaveFormReplyTemplateCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Subject).NotEmpty().MaximumLength(300);
        RuleFor(x => x.Body).NotEmpty();
    }
}
