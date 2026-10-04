using SharedKernel.Concrete;

namespace Domain.Entities.PublicForms;

public static class PublicFormSubmissionErrors
{
    public static readonly Error InvalidPage = Error.Failure("FormSubmission.InvalidPage", "A form submission needs a valid page id.");
    public static readonly Error EmptyFields = Error.Failure("FormSubmission.EmptyFields", "A form submission needs at least one field.");
    public static readonly Error TooLarge = Error.Failure("FormSubmission.TooLarge", "The submitted form data is too large.");
    public static readonly Error AttachmentPolicy = Error.Failure("FormSubmission.AttachmentPolicy", "An attachment exceeds what the form accepts.");
}
