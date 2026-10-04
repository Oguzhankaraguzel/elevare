using SharedKernel.Concrete;

namespace Application.Abstraction.Services;

/// <summary>
/// Reasons the CDN check could not be RUN. A CDN that answered badly is not one of
/// these — that comes back as a successful probe with an unhealthy result, because
/// "the CDN returned 404" is an answer the operator needs to see, not a failure of
/// the check itself.
/// </summary>
public static class CdnProbeErrors
{
    public static Error NotConfigured =>
        Error.Failure("CdnProbe.NotConfigured", "Enter a CDN base URL before testing it.");

    public static Error InvalidBaseUrl(string value) =>
        Error.Failure("CdnProbe.InvalidBaseUrl", $"'{value}' is not a valid http(s) address.");

    public static Error BlockedPrivateHost(string value) =>
        Error.Failure(
            "CdnProbe.BlockedPrivateHost",
            $"'{value}' points at a private or internal address. A CDN base URL must be a public host.");

    public static Error NoMediaToTest =>
        Error.Failure(
            "CdnProbe.NoMediaToTest",
            "There is no uploaded file to test with. Upload any image to the Media Library first — "
            + "the check works by pulling a real file through the CDN and comparing it with the original.");
}
