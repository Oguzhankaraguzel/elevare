using Domain.Entities.Logs;

namespace Application.Abstraction.Services;

/// <summary>
/// Writes rows to the <see cref="AuthEvent"/> audit trail (login/logout/expired
/// token). A thin wrapper around the DB write rather than a raw
/// <c>ICmsApplicationDbContext</c> call at each site, because every call site also
/// needs the current request's IP/user-agent, and duplicating that lookup three
/// times (login handler, logout endpoint, JWT bearer event) is exactly the kind of
/// copy-paste that drifts.
/// </summary>
public interface IAuthEventLogger
{
    /// <param name="saveImmediately">
    /// True (the default) writes the row to the database before returning — right
    /// for a caller like the logout endpoint that runs outside the command pipeline
    /// and has no other save coming. Pass false from inside a command handler whose
    /// pipeline already does a final SaveChanges (see <c>SaveChangesPipelineBehavior</c>)
    /// — the row is staged on the same DbContext either way, this only decides
    /// whether logging it costs its own extra database round trip.
    /// </param>
    Task LogAsync(
        AuthEventType eventType,
        Guid? userId,
        string userNameSnapshot,
        bool saveImmediately = true,
        CancellationToken cancellationToken = default);
}
