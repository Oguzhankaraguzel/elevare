using Application.Abstraction.Data;
using Application.Abstraction.Services;
using Domain.Entities.IntegrationSecrets;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Abstraction.Messaging;
using SharedKernel.Concrete;

namespace Application.Features.Commands.IntegrationSecrets.ClearIntegrationSecret;

internal sealed class ClearIntegrationSecretCommandHandler(ICmsApplicationDbContext db, IIntegrationSecretsChangeNotifier notifier)
    : ICommandHandler<ClearIntegrationSecretCommand>
{
    public async Task<Result> Handle(ClearIntegrationSecretCommand request, CancellationToken cancellationToken)
    {
        IntegrationSecret? secret = await db.IntegrationSecrets
            .FirstOrDefaultAsync(s => s.Key == request.Key && !s.IsDeleted, cancellationToken);

        if (secret is null)
            return Result.Failure(IntegrationSecretErrors.NotFound);

        secret.Value = null;
        secret.UpdateDate = DateTime.UtcNow;
        secret.UpdateUserId = request.UpdatedBy;

        // See UpdateIntegrationSecretCommandHandler for why this commits early
        // rather than waiting for the outer SaveChangesPipelineBehavior.
        await db.SaveChangesAsync(cancellationToken);

        return await notifier.NotifyAsync(request.Key, cancellationToken);
    }
}
