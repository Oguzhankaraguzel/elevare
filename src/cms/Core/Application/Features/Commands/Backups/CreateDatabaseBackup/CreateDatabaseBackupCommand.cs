using Application.Abstraction.Security;
using Domain.Entities.Permissions;
using SharedKernel.Abstraction.Messaging;

namespace Application.Features.Commands.Backups.CreateDatabaseBackup;

/// <summary>
/// Backs the "Back up now" button on <c>/admin/backups</c>. Queues the backup and
/// returns the scheduler's job id — it does not wait for the backup to complete.
/// </summary>
public sealed record CreateDatabaseBackupCommand : ICommand<string>, IRequirePermission
{
    public static string RequiredPermission => PermissionKeys.BackupCreate;
}
