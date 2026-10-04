using Application.Abstraction.Data;
using Application.Abstraction.Services;
using Domain.Entities.IntegrationSecrets;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Abstraction.Messaging;
using SharedKernel.Concrete;

namespace Application.Features.Commands.IntegrationSecrets.UpdateIntegrationSecret;

internal sealed class UpdateIntegrationSecretCommandHandler(
    ICmsApplicationDbContext db, IIntegrationSecretCrypto crypto, IIntegrationSecretsChangeNotifier notifier)
    : ICommandHandler<UpdateIntegrationSecretCommand>
{
    public async Task<Result> Handle(UpdateIntegrationSecretCommand request, CancellationToken cancellationToken)
    {
        IntegrationSecret? secret = await db.IntegrationSecrets
            .FirstOrDefaultAsync(s => s.Key == request.Key && !s.IsDeleted, cancellationToken);

        if (secret is null)
            return Result.Failure(IntegrationSecretErrors.NotFound);

        // Blank left in a masked field means "unchanged", not "clear" — the UI never
        // sends an empty Value for this command; ClearIntegrationSecretCommand is the
        // only path that blanks a secret, so a caller cannot do it by accident.
        if (string.IsNullOrEmpty(request.Value))
            return Result.Success();

        secret.Value = secret.IsSecret ? crypto.Protect(request.Value) : request.Value;
        secret.UpdateDate = DateTime.UtcNow;
        secret.UpdateUserId = request.UpdatedBy;

        // Committed here, explicitly and early, rather than left to the outer
        // SaveChangesPipelineBehavior (which only runs AFTER this handler returns)
        // — NotifyAsync below re-reads this row from the database, so it must see
        // what was just written, not the value that was there before this call.
        await db.SaveChangesAsync(cancellationToken);

        return await notifier.NotifyAsync(request.Key, cancellationToken);
    }
}
