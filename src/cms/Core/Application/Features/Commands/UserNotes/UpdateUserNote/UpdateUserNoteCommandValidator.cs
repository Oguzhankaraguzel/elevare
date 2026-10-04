using FluentValidation;

namespace Application.Features.Commands.UserNotes.UpdateUserNote;

internal sealed class UpdateUserNoteCommandValidator : AbstractValidator<UpdateUserNoteCommand>
{
    public UpdateUserNoteCommandValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Content).NotEmpty();
        RuleFor(x => x.Type).IsInEnum();
        RuleFor(x => x.Visibility).IsInEnum();
        RuleFor(x => x.Color).MaximumLength(20);
    }
}
