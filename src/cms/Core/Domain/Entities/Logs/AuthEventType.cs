namespace Domain.Entities.Logs;

public enum AuthEventType
{
    LoginSucceeded = 1,
    LoginFailed,
    Logout,

    /// <summary>
    /// A request arrived carrying a JWT whose own expiry had already passed — the
    /// browser still had it (the session cookie/idle timeout hadn't evicted it yet),
    /// but the token itself was stale. This is the observable half of "oturum
    /// bayatladı": true silent idle-eviction (nobody ever comes back) leaves nothing
    /// to log by definition, since no request ever arrives to notice it.
    /// </summary>
    SessionExpired,
}
