namespace WebMvc.Endpoints;

/// <param name="Reason">
/// Why a submission was refused, when it was for something the visitor can act on:
/// <c>too_large</c>, <c>too_many</c>, <c>type</c> (an attachment), <c>captcha</c>.
/// The runtime script turns it into a sentence in the page's language; null means
/// the generic message.
/// </param>
public sealed record FormSubmitResponse(bool Success, string? Reason = null);
