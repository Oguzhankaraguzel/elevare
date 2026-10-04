using Application.Abstraction.Services.Backup;
using SharedKernel.Abstraction.Messaging;
using SharedKernel.Concrete;

namespace Application.Features.Queries.Backups.GetDatabaseBackups;

internal sealed class GetDatabaseBackupsQueryHandler(IDatabaseBackupService backupService)
    : IQueryHandler<GetDatabaseBackupsQuery, List<DatabaseBackupInfo>>
{
    public Task<Result<List<DatabaseBackupInfo>>> Handle(
        GetDatabaseBackupsQuery request, CancellationToken cancellationToken) =>
        backupService.GetBackupsAsync(cancellationToken);
}
