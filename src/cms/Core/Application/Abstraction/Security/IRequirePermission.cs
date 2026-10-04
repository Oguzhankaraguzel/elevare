namespace Application.Abstraction.Security;

/// <summary>
/// Declares the permission a request needs before its handler may run.
/// <para>
/// Enforcement lives in <c>PermissionPipelineBehavior</c>, not in the handlers, so
/// a command states its requirement once and cannot be reached around. Hiding a
/// button or redirecting a page is presentation: it stops an honest user taking a
/// wrong turn, and stops nobody who types the URL or drives the component directly.
/// </para>
/// <para>
/// A request that does not implement this is deliberately open to any signed-in
/// user — reading one's own profile, logging in, loading the page list. Deciding
/// that is part of adding a request, which is why the interface is opt-in and
/// greppable rather than a default that can be silently inherited.
/// </para>
/// </summary>
public interface IRequirePermission
{
    /// <summary>A key from <c>PermissionKeys</c>.</summary>
    static abstract string RequiredPermission { get; }
}
