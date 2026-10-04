using Application.Abstraction.Data;
using Domain.Entities.IntegrationSecrets;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Abstraction.Messaging;
using SharedKernel.Concrete;

namespace Application.Features.Queries.IntegrationSecrets.GetIntegrationSecrets;

internal sealed class GetIntegrationSecretsQueryHandler(ICmsApplicationDbContext db)
    : IQueryHandler<GetIntegrationSecretsQuery, List<IntegrationSecretResponse>>
{
    public async Task<Result<List<IntegrationSecretResponse>>> Handle(GetIntegrationSecretsQuery request, CancellationToken cancellationToken)
    {
        List<IntegrationSecret> items = await db.IntegrationSecrets
            .AsNoTracking()
            .Where(s => !s.IsDeleted)
            .OrderBy(s => s.Category).ThenBy(s => s.Key)
            .ToListAsync(cancellationToken);

        List<IntegrationSecretResponse> result = [.. items.Select(s => new IntegrationSecretResponse(
            s.Id, s.Key,
            // The whole reason this table exists apart from SiteSettings: a secret's
            // value is never handed back once saved, only whether one is set.
            Value: s.IsSecret ? null : s.Value,
            IsSet: !string.IsNullOrEmpty(s.Value),
            s.DisplayName, s.Description, s.Category, s.IsSecret, s.IsSystem, s.DataType))];

        return Result.Success(result);
    }
}
