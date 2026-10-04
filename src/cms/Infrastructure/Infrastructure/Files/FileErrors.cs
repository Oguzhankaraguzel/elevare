using SharedKernel.Concrete;

namespace Infrastructure.Files;

/// <summary>Domain errors produced by <see cref="FileService"/>.</summary>
internal static class FileErrors
{
    public static readonly Error EmptyFile =
        new("File.Empty", "Uploaded file must not be empty.", ErrorType.Validation);

    public static Error FileTooLarge(long maxBytes) =>
        new("File.TooLarge", $"File size exceeds the {maxBytes / 1024 / 1024} MB limit.", ErrorType.Validation);

    public static Error MimeTypeNotAllowed(string mimeType) =>
        new("File.MimeTypeNotAllowed", $"'{mimeType}' is not an allowed file type.", ErrorType.Validation);

    /// <summary>
    /// The file name's extension does not describe the content type that was declared.
    /// Refused rather than corrected: the serving endpoint decides a response's
    /// Content-Type from the extension, so a mismatch here is how a file passes an
    /// "image/png" check and is later served as text/html from our own origin.
    /// </summary>
    public static Error ExtensionMismatch(string fileName, string mimeType) =>
        new("File.ExtensionMismatch",
            $"The extension of '{fileName}' does not match the declared type '{mimeType}'.",
            ErrorType.Validation);

    public static Error NotFound(int id) =>
        new("File.NotFound", $"File with ID {id} was not found.", ErrorType.NotFound);

    public static Error PhysicalFileNotFound(string fileName) =>
        new("File.PhysicalNotFound", $"Physical file '{fileName}' is missing from storage.", ErrorType.Problem);

    public static Error UploadFailed(string reason) =>
        new("File.UploadFailed", $"Upload failed: {reason}", ErrorType.Problem);
}
