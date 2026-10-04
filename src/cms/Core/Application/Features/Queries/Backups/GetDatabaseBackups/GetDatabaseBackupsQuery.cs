using Application.Abstraction.Security;
using Domain.Entities.Permissions;
using Application.Abstraction.Services.Backup;
using SharedKernel.Abstraction.Messaging;

namespace Application.Features.Queries.Backups.GetDatabaseBackups;

/// <summary>Backs the backup list on <c>/admin/backups</c> — see <c>IDatabaseBackupService</c>.</summary>
public sealed record GetDatabaseBackupsQuery : IQuery<List<DatabaseBackupInfo>>, IRequirePermission
{
    public static string RequiredPermission => PermissionKeys.BackupCreate;
}

