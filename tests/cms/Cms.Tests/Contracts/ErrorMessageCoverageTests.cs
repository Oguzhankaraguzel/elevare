using System.Reflection;
using System.Resources;
using System.Text.RegularExpressions;
using SharedKernel.Concrete;
using Shouldly;

namespace Cms.Tests.Contracts;

/// <summary>
/// Every domain error the CMS can raise has to be translatable. When a code has no
/// row in ErrorMessages.resx, <c>CmsLocalizer.Error</c> falls back to the raw
/// <see cref="Error.Description"/> — which is written in English, so Turkish users
/// get an English toast. That is invisible in code review and only shows up in the
/// one screen where the error happens, which is why it is asserted here instead.
/// </summary>
public sealed class ErrorMessageCoverageTests
{
    /// <summary>
    /// Codes whose whole message IS a runtime detail — a storage driver's exception
    /// text, an Identity validation list, how many rows a trim managed before it
    /// failed. A fixed translation would replace that detail with a sentence that
    /// says less, so these are deliberately left to fall back.
    /// </summary>
    private static readonly HashSet<string> DiagnosticOnly =
    [
        "Backup.Failed",
        "BlobStorage.SaveFailed",
        "BlobStorage.ReadFailed",
        "BlobStorage.DeleteFailed",
        "BlobStorage.UrlResolutionFailed",
        "Retention.TrimFailed",
        "RolePermission.RefreshFailed",
        "Role.CreateFailed",
        "Role.DeleteFailed",
        "User.CreateFailed",
    ];

    private static IEnumerable<string> DeclaredErrorCodes()
    {
        // Read off the *_Errors static classes rather than by parsing source: a code
        // renamed in C# has to be renamed here too, and reflection notices that.
        Assembly domain = typeof(Domain.Entities.PageInfos.PageInfoErrors).Assembly;

        foreach (Type type in domain.GetTypes().Where(t => t.IsAbstract && t.IsSealed))
        {
            foreach (FieldInfo field in type.GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                if (field.FieldType == typeof(Error) && field.GetValue(null) is Error error)
                    yield return error.Code;
            }
        }
    }

    /// <summary>
    /// Codes that are never declared as a static field — built inline at the call
    /// site, or handed to FluentValidation's WithErrorCode. Reflection cannot see
    /// them, and the first one added slipped through and shipped an untranslated
    /// message, so they are listed explicitly instead.
    /// </summary>
    private static readonly string[] InlineCodes =
    [
        "Database.SaveError",
        "Page.StructuredDataInvalidJson",
    ];

    [Fact]
    public void Every_inline_error_code_has_a_turkish_message_too()
    {
        ResourceManager resources = new(
            "Wasm.Resources.ErrorMessages", typeof(Wasm.Resources.ErrorMessages).Assembly);

        List<string> untranslated = [.. InlineCodes
            .Where(code => string.IsNullOrEmpty(
                resources.GetString(code, new System.Globalization.CultureInfo("tr"))))];

        untranslated.ShouldBeEmpty(
            $"these inline error codes would show an English message to a Turkish user: {string.Join(", ", untranslated)}");
    }

    [Fact]
    public void The_reflection_actually_finds_the_error_catalogue()
    {
        // Without this the two tests below pass by finding nothing at all — a
        // namespace move or a change to how errors are declared would quietly turn
        // them into assertions about an empty list.
        // (About 70 once the never-raised ones were removed; the floor only has to
        // be well above zero.)
        DeclaredErrorCodes().Distinct().Count().ShouldBeGreaterThan(50);
    }

    [Fact]
    public void Every_declared_error_code_has_a_turkish_message()
    {
        ResourceManager resources = new(
            "Wasm.Resources.ErrorMessages", typeof(Wasm.Resources.ErrorMessages).Assembly);

        List<string> untranslated = [.. DeclaredErrorCodes()
            .Where(code => !DiagnosticOnly.Contains(code))
            .Distinct()
            .Where(code => string.IsNullOrEmpty(resources.GetString(code, new System.Globalization.CultureInfo("tr"))))
            .OrderBy(code => code, StringComparer.Ordinal)];

        untranslated.ShouldBeEmpty(
            $"these error codes would show an English message to a Turkish user: {string.Join(", ", untranslated)}");
    }

    [Fact]
    public void No_error_carries_a_placeholder_code_or_an_empty_message()
    {
        // Guards the shape of mistake that shipped as new Error("a", "") — a code
        // nobody can translate, attached to a toast that renders blank.
        List<string> broken = [.. DeclaredErrorCodes()
            .Where(code => code.Length < 4 || !Regex.IsMatch(code, @"^[A-Za-z]+\.[A-Za-z]+$"))
            .Distinct()];

        broken.ShouldBeEmpty($"error codes must read as Area.Reason: {string.Join(", ", broken)}");
    }
}
