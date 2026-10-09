using Application.Abstraction.Data;
using Application.Abstraction.Services;
using Domain.Entities.IntegrationSecrets;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Abstraction.Messaging;
using SharedKernel.Concrete;

namespace Application.Features.Queries.IntegrationSecrets.GetIntegrationSecrets;

internal sealed class GetIntegrationSecretsQueryHandler(ICmsApplicationDbContext db, IConfigurationInspector configuration)
    : IQueryHandler<GetIntegrationSecretsQuery, List<IntegrationSecretResponse>>
{
    /// <summary>
    /// What a choice runs on when neither the screen nor the server sets it — the
    /// defaults of EmailOptions.EnableSsl and ObjectStorageOptions.Provider, which
    /// live in Infrastructure and so cannot be read from here.
    /// </summary>
    private static readonly Dictionary<string, string> ChoiceDefaults = new(StringComparer.Ordinal)
    {
        ["Email:EnableSsl"] = "true",
        ["ObjectStorage:Provider"] = "Local",
    };

    public async Task<Result<List<IntegrationSecretResponse>>> Handle(GetIntegrationSecretsQuery request, CancellationToken cancellationToken)
    {
        List<IntegrationSecret> items = await db.IntegrationSecrets
            .AsNoTracking()
            .Where(s => !s.IsDeleted)
            .OrderBy(s => s.Category).ThenBy(s => s.Key)
            .ToListAsync(cancellationToken);

        List<IntegrationSecretResponse> result = [.. items.Select(s =>
        {
            bool isSet = !string.IsNullOrEmpty(s.Value);
            string? serverValue = configuration.GetServerValue(s.Key);

            // Nothing typed into a field ever comes back to the browser — not only
            // passwords: a mail host, user name or bucket is still a map of the
            // infrastructure, and the screen only needs to know whether one is there.
            string? shown = null;
            if (IntegrationSecretResponse.IsChoiceType(s.DataType))
            {
                string? inEffect = isSet ? s.Value : serverValue;
                shown = inEffect ?? ChoiceDefaults.GetValueOrDefault(s.Key);
            }

            return new IntegrationSecretResponse(
                s.Id, s.Key, shown, isSet, HasServerValue: serverValue is not null,
                s.DisplayName, s.Description, s.Category, s.IsSecret, s.IsSystem, s.DataType);
        })];

        return Result.Success(result);
    }
}
