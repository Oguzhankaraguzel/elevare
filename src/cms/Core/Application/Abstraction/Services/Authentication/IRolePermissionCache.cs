using SharedKernel.Concrete;

namespace Application.Abstraction.Services.Authentication;

/// <summary>
/// In-memory snapshot of "which permission keys does each role grant", refreshed
/// whenever a SuperAdmin edits a role.
/// <para>
/// This exists so a permission change takes effect immediately for users who are
/// already signed in. Permissions used to be read straight off the sign-in
/// principal (Identity copies AspNetRoleClaims into it once, at sign-in), which
/// meant an admin removing a permission had no effect until every affected user
/// logged out and back in — a genuine security gap, since revocation is exactly
/// the case that must not wait.
/// </para>
/// <para>
/// The lookup is deliberately synchronous and allocation-light: <c>HasPermission</c>
/// is called from Razor markup on nearly every render, so it must never touch the
/// database on the hot path. Reads hit the current snapshot; only
/// <see cref="RefreshAsync"/> queries.
/// </para>
/// </summary>
public interface IRolePermissionCache
{
    /// <summary>
    /// True when any of <paramref name="roleNames"/> grants <paramref name="permissionKey"/>.
    /// Reads the current snapshot; never blocks on I/O.
    /// <para>
    /// Deliberately a plain <see langword="bool"/> rather than a <see cref="Result"/>:
    /// this is an authorisation decision on the hot path with exactly two safe answers.
    /// An "errored" third state would have to be collapsed back into deny at every one
    /// of the dozens of call sites, and the first one that got it wrong would be a
    /// privilege escalation. There is nothing to report anyway — the snapshot is already
    /// in memory, and a failure to BUILD it is reported by <see cref="RefreshAsync"/>.
    /// </para>
    /// </summary>
    bool IsGranted(IEnumerable<string> roleNames, string permissionKey);

    /// <summary>
    /// Reloads the snapshot from the role-claims store. Awaited by the commands that
    /// change roles, so the admin who made the change sees it applied on their very
    /// next interaction.
    /// <para>
    /// Returns a <see cref="Result"/> because a silent failure here is a security
    /// problem, not a cosmetic one: the role edit is already committed, so if the
    /// snapshot is not rebuilt the admin is told the permission changed while every
    /// signed-in user keeps the old one. The caller must surface that.
    /// </para>
    /// </summary>
    Task<Result> RefreshAsync(CancellationToken cancellationToken = default);
}
