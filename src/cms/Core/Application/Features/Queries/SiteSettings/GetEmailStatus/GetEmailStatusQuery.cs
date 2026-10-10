using SharedKernel.Abstraction.Messaging;

namespace Application.Features.Queries.SiteSettings.GetEmailStatus;

/// <summary>
/// Whether this CMS can send mail at all — true when a mail server and a sender are
/// configured, whether a send would succeed or not. Lets the screens that depend on
/// mail say so before anyone relies on it: creating a user, "forgot password".
/// Not permission-gated, because the sign-in screen asks it; the answer reveals
/// nothing a failed "forgot password" would not.
/// </summary>
public sealed record GetEmailStatusQuery : IQuery<bool>;
