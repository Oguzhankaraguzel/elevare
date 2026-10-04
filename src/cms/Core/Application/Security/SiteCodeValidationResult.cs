using SharedKernel.Concrete;

namespace Application.Security;

/// <summary>
/// Outcome of <see cref="SiteCodeValidator"/>.
/// <para>
/// Warnings are carried separately from the failure because they mean different
/// things to the author: an <see cref="Error"/> stops the save, a warning is
/// something they should know about but may well have intended. Collapsing the two
/// would force a choice between blocking legitimate vendor snippets and saying
/// nothing at all.
/// </para>
/// </summary>
public sealed record SiteCodeValidationResult(bool IsValid, Error? Error, IReadOnlyList<string> Warnings)
{
    public static SiteCodeValidationResult Valid(IReadOnlyList<string> warnings) => new(true, null, warnings);

    public static SiteCodeValidationResult Invalid(Error error) => new(false, error, []);
}
