using Application.Abstraction.Security;
using Domain.Entities.Permissions;
using SharedKernel.Abstraction.Messaging;

namespace Application.Features.Queries.IntegrationSecrets.GetIntegrationSecrets;

public sealed record GetIntegrationSecretsQuery : IQuery<List<IntegrationSecretResponse>>, IRequirePermission
{
    public static string RequiredPermission => PermissionKeys.SecretsManage;
}
