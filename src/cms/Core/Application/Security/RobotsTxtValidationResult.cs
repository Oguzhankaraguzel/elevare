using SharedKernel.Concrete;

namespace Application.Security;

/// <summary>
/// Outcome of <see cref="RobotsTxtValidator"/>. Warnings do not block the save —
/// blocking the whole site is a legitimate thing to want on a staging domain.
/// </summary>
public sealed record RobotsTxtValidationResult(
    bool IsValid,
    Error? Error,
    IReadOnlyList<string> Warnings)
{
    public static RobotsTxtValidationResult Valid(IReadOnlyList<string> warnings) =>
        new(true, null, warnings);

    public static RobotsTxtValidationResult Invalid(Error error) =>
        new(false, error, []);
}
