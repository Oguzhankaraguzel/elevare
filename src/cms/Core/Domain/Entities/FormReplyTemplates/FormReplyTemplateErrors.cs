using SharedKernel.Concrete;

namespace Domain.Entities.FormReplyTemplates;

public static class FormReplyTemplateErrors
{
    public static readonly Error NotFound = Error.NotFound("FormReplyTemplate.NotFound", "The reply template was not found.");
}
