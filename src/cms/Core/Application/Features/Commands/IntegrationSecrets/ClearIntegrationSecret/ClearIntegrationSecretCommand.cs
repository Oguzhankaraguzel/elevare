using Application.Abstraction.Security;
using Domain.Entities.Permissions;
using SharedKernel.Abstraction.Messaging;

namespace Application.Features.Commands.IntegrationSecrets.ClearIntegrationSecret;

public sealed record ClearIntegrationSecretCommand(string Key, Guid UpdatedBy)
    : ICommand, IRequirePermission
{
    public static string RequiredPermission => PermissionKeys.SecretsManage;
}
