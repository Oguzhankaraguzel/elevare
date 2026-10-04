using SharedKernel.Concrete;

namespace Application.Abstraction.Services;

/// <summary>
/// Verifies that a pull CDN is actually serving this site's media.
/// <para>
/// <c>Integrations.CdnBaseUrl</c> is applied to every new upload's public URL the
/// moment it is saved, and nothing else checks it — a typo, a CDN pointed at the
/// wrong origin, or a zone that has not finished provisioning all produce the same
/// silent outcome: every image uploaded from then on is broken on the live site,
/// and the CMS shows no sign of it because the CMS never fetches those URLs.
/// This is the check that turns that into an answer before the setting is saved.
/// </para>
/// </summary>
public interface ICdnProbe
{
    /// <summary>
    /// Fetches a real, already-uploaded media file through <paramref name="cdnBaseUrl"/>
    /// and compares what comes back with the copy on disk.
    /// <para>
    /// Takes the base URL as an argument rather than reading the setting, so the value
    /// can be tested BEFORE it is saved — testing only what is already saved would mean
    /// every operator has to publish a broken CDN prefix to find out it is broken.
    /// </para>
    /// </summary>
    // string, not Uri: the value is raw text an operator just typed into a settings
    // field and has NOT been validated — "is this even a URL" is one of the questions
    // this method answers, so a Uri parameter would move that failure to the caller
    // and lose the message the operator needs.
#pragma warning disable CA1054
    Task<Result<CdnProbeResult>> CheckAsync(string cdnBaseUrl, CancellationToken cancellationToken = default);
#pragma warning restore CA1054
}
