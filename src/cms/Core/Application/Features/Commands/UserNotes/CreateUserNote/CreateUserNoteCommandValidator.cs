using FluentValidation;

namespace Application.Features.Commands.UserNotes.CreateUserNote;

internal sealed class CreateUserNoteCommandValidator : AbstractValidator<CreateUserNoteCommand>
{
    public CreateUserNoteCommandValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Content).NotEmpty();
        RuleFor(x => x.Type).IsInEnum();
        RuleFor(x => x.Visibility).IsInEnum();
        RuleFor(x => x.Color).MaximumLength(20);
    }
}
