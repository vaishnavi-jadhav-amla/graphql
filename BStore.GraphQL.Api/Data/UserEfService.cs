using BStore.GraphQL.Api.GraphQL.Types;
using HotChocolate;
using ErrorCodes = BStore.GraphQL.Api.Common.ErrorCodes;
using Microsoft.EntityFrameworkCore;
using Znode.Libraries.Data.DataModel;
using Znode.Libraries.Data.ZnodeEntity;

namespace BStore.GraphQL.Api.Data;

public sealed class UserEfService(Znode_Entities db) : IUserDataService
{
    public async Task<UserListPage?> GetUsersPagedAsync(int page, int pageSize, CancellationToken ct)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Max(pageSize, 1);

        var total = await db.ZnodeUsers.AsNoTracking().CountAsync(ct);
        var rows = await db.ZnodeUsers.AsNoTracking()
            .OrderBy(u => u.UserId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(u => new UserRow
            {
                UserId      = u.UserId,
                FirstName   = u.FirstName ?? "",
                LastName    = u.LastName ?? "",
                Email       = u.Email ?? "",
                UserName    = u.UserName,
                PhoneNumber = u.PhoneNumber,
                RoleName    = null,
                IsActive    = u.IsActive,
                ExternalId  = u.ExternalId
            })
            .ToListAsync(ct);

        return new UserListPage
        {
            Users    = rows,
            Total    = total,
            Page     = page,
            PageSize = pageSize
        };
    }

    public async Task<UserRow?> GetUserAsync(int userId, CancellationToken ct)
    {
        var u = await db.ZnodeUsers.AsNoTracking()
            .FirstOrDefaultAsync(x => x.UserId == userId, ct);
        return u is null ? null : MapUser(u, await GetPrimaryRoleNameAsync(u.AspNetUserId, ct));
    }

    public async Task<UserRow?> GetUserByUsernameAsync(string username, string storeCode, CancellationToken ct)
    {
        var q =
            from zu in db.ZnodeUsers.AsNoTracking()
            join up in db.ZnodeUserPortals.AsNoTracking() on zu.UserId equals up.UserId
            join p in db.ZnodePortals.AsNoTracking() on up.PortalId equals p.PortalId
            where zu.UserName == username && p.StoreCode == storeCode
            select zu;

        var row = await q.FirstOrDefaultAsync(ct);
        return row is null ? null : MapUser(row, await GetPrimaryRoleNameAsync(row.AspNetUserId, ct));
    }

    public Task<UserRow?> CreateUserAsync(UserCreateInput input, CancellationToken ct) =>
        throw new GraphQLException(ErrorBuilder.New()
            .SetMessage(
                "User creation is not implemented in the GraphQL database layer (requires AspNet Identity and Znode user pipeline).")
            .SetCode(ErrorCodes.NotSupportedDbOperation)
            .Build());

    public async Task<bool> UpdateUserAsync(UserUpdateInput input, CancellationToken ct)
    {
        var u = await db.ZnodeUsers.FirstOrDefaultAsync(x => x.UserId == input.UserId, ct);
        if (u is null)
            return false;

        if (!string.IsNullOrWhiteSpace(input.FirstName))
            u.FirstName = input.FirstName;
        if (!string.IsNullOrWhiteSpace(input.LastName))
            u.LastName = input.LastName;
        if (!string.IsNullOrWhiteSpace(input.Email))
            u.Email = input.Email;
        if (input.PhoneNumber is not null)
            u.PhoneNumber = input.PhoneNumber;
        if (input.ExternalId is not null)
            u.ExternalId = input.ExternalId;

        u.ModifiedDate = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> ToggleUserActiveAsync(int userId, bool lockUser, CancellationToken ct)
    {
        var u = await db.ZnodeUsers.FirstOrDefaultAsync(x => x.UserId == userId, ct);
        if (u is null)
            return false;

        u.IsActive     = !lockUser;
        u.ModifiedDate = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return true;
    }

    public Task<bool> DeleteUserAsync(string id, CancellationToken ct) =>
        throw new GraphQLException(ErrorBuilder.New()
            .SetMessage(
                "User delete is not implemented in the GraphQL database layer (requires AspNet Identity and related cleanup).")
            .SetCode(ErrorCodes.NotSupportedDbOperation)
            .Build());

    private static UserRow MapUser(ZnodeUser u, string? roleName) =>
        new()
        {
            UserId      = u.UserId,
            FirstName   = u.FirstName ?? "",
            LastName    = u.LastName ?? "",
            Email       = u.Email ?? "",
            UserName    = u.UserName,
            PhoneNumber = u.PhoneNumber,
            RoleName    = roleName,
            IsActive    = u.IsActive,
            ExternalId  = u.ExternalId
        };

    private async Task<string?> GetPrimaryRoleNameAsync(string? aspNetUserId, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(aspNetUserId))
            return null;

        return await (
                from u in db.AspNetUsers.AsNoTracking()
                where u.Id == aspNetUserId
                from r in u.Roles
                orderby r.Name
                select r.Name)
            .FirstOrDefaultAsync(ct);
    }
}
