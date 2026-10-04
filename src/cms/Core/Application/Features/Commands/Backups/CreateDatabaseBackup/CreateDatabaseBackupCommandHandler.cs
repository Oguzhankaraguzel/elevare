using Application.Abstraction.Services.Backup;
using SharedKernel.Abstraction.Messaging;
using SharedKernel.Concrete;

namespace Application.Features.Commands.Backups.CreateDatabaseBackup;

internal sealed class CreateDatabaseBackupCommandHandler(IBackupJobLauncher jobLauncher)
    : ICommandHandler<CreateDatabaseBackupCommand, string>
{
    public Task<Result<string>> Handle(CreateDatabaseBackupCommand request, CancellationToken cancellationToken) =>
        Task.FromResult(jobLauncher.EnqueueBackup());
}
