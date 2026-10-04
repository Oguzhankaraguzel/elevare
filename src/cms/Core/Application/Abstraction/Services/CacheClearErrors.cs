using SharedKernel.Concrete;

namespace Application.Abstraction.Services;

/// <summary>Failures from <see cref="ICacheClearService"/>.</summary>
public static class CacheClearErrors
{
    public static readonly Error StatusUnreadable =
        Error.Failure("Cache.StatusUnreadable", "The website returned an unexpected response.");

    public static readonly Error SecretMismatch =
        Error.Failure("Cache.SecretMismatch", "The website rejected the request (401). Cache:ClearSecret must be identical, and non-empty, on both the CMS and the website.");

    public static Error BaseUrlNotConfigured(string settingKey) =>
        Error.Failure("Cache.BaseUrlNotConfigured", $"'{settingKey}' is empty. The cache cannot be managed until the public site address is set.");

    public static Error RequestFailed(int statusCode, string? reason) =>
        Error.Failure("Cache.RequestFailed", $"The website returned {statusCode} ({reason}).");

    public static Error Unreachable(Exception ex) =>
        Error.Failure("Cache.Unreachable", $"The website could not be reached — {ex?.GetType().Name}: {ex?.Message}");
}
