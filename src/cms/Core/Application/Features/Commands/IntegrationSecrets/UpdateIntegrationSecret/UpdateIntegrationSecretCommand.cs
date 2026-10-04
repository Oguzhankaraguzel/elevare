using Application.Abstraction.Security;
using Domain.Entities.Permissions;
using SharedKernel.Abstraction.Messaging;

namespace Application.Features.Commands.IntegrationSecrets.UpdateIntegrationSecret;

/// <param name="Value">
/// The new value. For a secret field, an empty submission is rejected rather than
/// silently accepted — see <see cref="Commands.IntegrationSecrets.ClearIntegrationSecret.ClearIntegrationSecretCommand"/>
/// for the explicit way to blank one out, which the UI never reaches by accident.
/// </param>
public sealed record UpdateIntegrationSecretCommand(string Key, string Value, Guid UpdatedBy)
    : ICommand, IRequirePermission
{
    public static string RequiredPermission => PermissionKeys.SecretsManage;
}
