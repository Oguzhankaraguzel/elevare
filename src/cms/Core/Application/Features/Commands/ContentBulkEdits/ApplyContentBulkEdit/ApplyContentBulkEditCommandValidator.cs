using Domain.Entities.ContentBulkEdits;
using FluentValidation;

namespace Application.Features.Commands.ContentBulkEdits.ApplyContentBulkEdit;

internal sealed class ApplyContentBulkEditCommandValidator : AbstractValidator<ApplyContentBulkEditCommand>
{
    public ApplyContentBulkEditCommandValidator()
    {
        RuleFor(x => x.SearchText).NotEmpty().WithMessage(ContentBulkEditErrors.SearchTextRequired.Description);
        RuleFor(x => x.ReplaceText).NotNull();
        RuleFor(x => x.PageInfoIds).NotEmpty().WithMessage(ContentBulkEditErrors.NoPagesSelected.Description);
    }
}
