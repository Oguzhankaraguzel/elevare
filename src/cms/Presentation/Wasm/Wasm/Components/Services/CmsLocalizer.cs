using Microsoft.Extensions.Localization;
using SharedKernel.Concrete;
using Wasm.Resources;

namespace Wasm.Components.Services;

/// <summary>
/// Facade service that provides localised UI strings and domain error messages.
/// Inject as <c>@inject CmsLocalizer L</c> in Blazor components.
/// </summary>
public sealed class CmsLocalizer
{
    private readonly IStringLocalizer<CmsMessages> _ui;
    private readonly IStringLocalizer<ErrorMessages> _errors;

    public CmsLocalizer(
        IStringLocalizer<CmsMessages> ui,
        IStringLocalizer<ErrorMessages> errors)
    {
        _ui = ui;
        _errors = errors;
    }

    /// <summary>Returns the localised UI string for <paramref name="key"/>.</summary>
    public string this[string key] => _ui[key].Value;

    /// <summary>
    /// Returns the localised message for a domain <see cref="Error"/>.
    /// Falls back to <see cref="Error.Description"/> when the code has no translation.
    /// </summary>
    public string Error(Error error)
    {
        // A ValidationError's own description is the useless summary "One or more
        // validation errors occurred" — the sentence that tells the user which field
        // is wrong is in its children. Showing the wrapper meant every form in the
        // CMS refused a save without ever saying why.
        if (error is ValidationError { Errors.Length: > 0 } validation)
            return string.Join(" ", validation.Errors.Select(Error));

        LocalizedString localised = _errors[error.Code];
        return localised.ResourceNotFound ? error.Description : localised.Value;
    }
}
