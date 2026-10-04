using SharedKernel.Concrete;

namespace Domain.Entities.FormSubmissions;

public static class FormSubmissionErrors
{
    public static readonly Error NotFound = Error.NotFound("FormSubmission.NotFound", "The form submission was not found.");
}
