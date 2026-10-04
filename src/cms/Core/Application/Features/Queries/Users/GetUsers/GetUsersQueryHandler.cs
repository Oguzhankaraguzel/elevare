using Application.Abstraction.Data;
using SharedKernel.Abstraction.Messaging;
using Domain.Entities.Users;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Concrete;
using SharedKernel.Extensions.Linq;

namespace Application.Features.Queries.Users.GetUsers;

internal sealed record GetUsersQueryHandler(UserManager<AppUser> UserManager, ICmsApplicationDbContext Db)
    : IQueryHandler<GetUsersQuery, PagedResult<UserResponse>>
{
    public async Task<Result<PagedResult<UserResponse>>> Handle(GetUsersQuery request, CancellationToken cancellationToken)
    {
        IQueryable<AppUser> query = UserManager.Users
            .WhereIf(request.Id.HasValue, u => u.Id == request.Id!.Value)
            // Lower-cased both sides — LIKE is case-sensitive in PostgreSQL. Full
            // reasoning and the analyzer suppressions: SqlSearchProvider.
#pragma warning disable CA1304, CA1308, CA1311, CA1862
            .WhereIf(!string.IsNullOrWhiteSpace(request.Email), u => u.Email!.ToLower().Contains(request.Email!.ToLowerInvariant()))
            .WhereIf(!string.IsNullOrWhiteSpace(request.UserName), u => u.UserName!.ToLower().Contains(request.UserName!.ToLowerInvariant()))
#pragma warning restore CA1304, CA1308, CA1311, CA1862
            .WhereIf(request.IsActive.HasValue, u => u.IsActive == request.IsActive!.Value)
            .WhereIf(request.EmailConfirmed.HasValue, u => u.EmailConfirmed == request.EmailConfirmed!.Value)
            .WhereIf(request.CreatedAfter.HasValue, u => u.CreateDate >= request.CreatedAfter!.Value)
            .WhereIf(request.CreatedBefore.HasValue, u => u.CreateDate <= request.CreatedBefore!.Value)
            .WhereIf(request.LastLoginAfter.HasValue, u => u.LastLoginDate >= request.LastLoginAfter!.Value);

        // If filtering by role, use in-memory approach (Identity doesn't support IQueryable joins easily)
        if (!string.IsNullOrWhiteSpace(request.Role))
        {
            IList<AppUser> roleUsers = await UserManager.GetUsersInRoleAsync(request.Role!);
            var roleUserIds = roleUsers.Select(u => u.Id).ToHashSet();
            query = query.Where(u => roleUserIds.Contains(u.Id));
        }

        // Apply ordering
        query = request.OrderBy.ToUpperInvariant() switch
        {
            "EMAIL" => request.OrderDesc ? query.OrderByDescending(u => u.Email) : query.OrderBy(u => u.Email),
            "USERNAME" => request.OrderDesc ? query.OrderByDescending(u => u.UserName) : query.OrderBy(u => u.UserName),
            "LASTNAME" => request.OrderDesc ? query.OrderByDescending(u => u.LastName) : query.OrderBy(u => u.LastName),
            "LASTLOGINDATE" => request.OrderDesc ? query.OrderByDescending(u => u.LastLoginDate) : query.OrderBy(u => u.LastLoginDate),
            _ => request.OrderDesc ? query.OrderByDescending(u => u.CreateDate) : query.OrderBy(u => u.CreateDate)
        };

        int totalCount = await query.CountAsync(cancellationToken);

        List<AppUser> users = await query
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        // Latest outstanding setup-link expiry per user without a password yet — one
        // batch query instead of N, grouped in memory (a CMS's user list is small).
        Guid[] passwordlessIds = [.. users.Where(u => u.PasswordHash is null).Select(u => u.Id)];
        Dictionary<Guid, DateTime> latestSetupExpiry = passwordlessIds.Length == 0
            ? []
            : (await Db.PasswordSetupTokens
                .Where(t => passwordlessIds.Contains(t.UserId) && t.ConsumedAtUtc == null)
                .ToListAsync(cancellationToken))
                .GroupBy(t => t.UserId)
                .ToDictionary(g => g.Key, g => g.Max(t => t.ExpiresAtUtc));

        // Load roles for each user
        List<UserResponse> responses = new(users.Count);
        foreach (AppUser user in users)
        {
            IList<string> roles = await UserManager.GetRolesAsync(user);
            bool hasPassword = user.PasswordHash is not null;
            DateTime? pendingExpiry = !hasPassword && latestSetupExpiry.TryGetValue(user.Id, out DateTime expiry)
                ? expiry
                : null;
            responses.Add(new UserResponse(
                Id: user.Id,
                Email: user.Email,
                UserName: user.UserName,
                FirstName: user.FirstName,
                LastName: user.LastName,
                FullName: user.FullName,
                Avatar: user.AvatarUrl,
                Bio: user.Bio,
                IsActive: user.IsActive,
                EmailConfirmed: user.EmailConfirmed,
                CreateDate: user.CreateDate,
                LastLoginDate: user.LastLoginDate,
                Roles: roles.ToList().AsReadOnly(),
                HasPassword: hasPassword,
                PendingSetupExpiresAtUtc: pendingExpiry));
        }

        return Result.Success(PagedResult<UserResponse>.Create(responses, totalCount, request.Page, request.PageSize));
    }
}
