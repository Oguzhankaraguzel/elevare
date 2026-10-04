namespace Application.Features.Queries.RoleManagement.GetRoles;

public sealed record RoleResponse(Guid Id, string Name, bool IsBuiltIn, List<string> Permissions);
