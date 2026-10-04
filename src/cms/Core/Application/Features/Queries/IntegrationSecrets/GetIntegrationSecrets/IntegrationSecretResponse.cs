using Domain.Entities.IntegrationSecrets;

namespace Application.Features.Queries.IntegrationSecrets.GetIntegrationSecrets;

/// <param name="Value">
/// Null whenever <see cref="IsSecret"/> is true — a secret's value never leaves the
/// server once saved. <see cref="IsSet"/> is what the UI renders instead (a masked
/// placeholder vs. an empty field).
/// </param>
public sealed record IntegrationSecretResponse(
    int Id, string Key, string? Value, bool IsSet, string DisplayName,
    string? Description, IntegrationSecretCategory Category, bool IsSecret,
    bool IsSystem, string? DataType);
