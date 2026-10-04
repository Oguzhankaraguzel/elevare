using SharedKernel.Abstraction.Messaging;

namespace Application.Features.Queries.RoleManagement.GetRoles;

public sealed record GetRolesQuery : IQuery<List<RoleResponse>>;
