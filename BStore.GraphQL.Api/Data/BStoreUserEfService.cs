using BStore.GraphQL.Api.GraphQL.Types;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Data;
using Znode.Libraries.Data.DataModel;
using Znode.Libraries.Data.ZnodeEntity;

namespace BStore.GraphQL.Api.Data;

public sealed class BStoreUserEfService(
    Znode_Entities db,
    ILogger<BStoreUserEfService> log) : IBStoreUserDataService
{
    private const string BStoreManagerRoleToken = "BStoreManager";
    private const string BStoreOwnerRoleToken   = "BStoreOwner";

    public async Task<BStoreUserRoleRow?> GetUserRoleAccessAsync(int userId, CancellationToken ct)
    {
        if (userId == 0)
            return null;

        var aspNetUserId = await db.ZnodeUsers.AsNoTracking()
            .Where(u => u.UserId == userId)
            .Select(u => u.AspNetUserId)
            .FirstOrDefaultAsync(ct);
        if (string.IsNullOrEmpty(aspNetUserId))
            return new BStoreUserRoleRow { UserId = userId, IsManager = false, IsOwner = false };

        var roleNames = await (
                from u in db.AspNetUsers.AsNoTracking()
                where u.Id == aspNetUserId
                from r in u.Roles
                select r.Name)
            .ToListAsync(ct);

        var isManager = roleNames.Any(n =>
            n.Contains(BStoreManagerRoleToken, StringComparison.OrdinalIgnoreCase));
        var isOwner = roleNames.Any(n =>
            n.Contains(BStoreOwnerRoleToken, StringComparison.OrdinalIgnoreCase));

        return new BStoreUserRoleRow { UserId = userId, IsManager = isManager, IsOwner = isOwner };
    }

    public async Task<BStoreUserAccessListResult?> GetUserAccessListAsync(
        int userId, bool isAssociated, int pageIndex, int pageSize, CancellationToken ct)
    {
        pageIndex = Math.Max(pageIndex, 1);
        pageSize  = Math.Max(pageSize, 1);

        IQueryable<BStoreUserAccessRow> rowQuery;

        if (isAssociated)
        {
            rowQuery =
                from r in db.ZnodeBStoresUserPortalRoles.AsNoTracking()
                join p in db.ZnodePortals.AsNoTracking() on r.BStorePortalId equals p.PortalId
                where r.UserId == userId && p.IsBStore
                orderby p.StoreName
                select new BStoreUserAccessRow
                {
                    PortalId     = p.PortalId,
                    StoreName    = p.StoreName ?? "",
                    IsActive     = p.IsActive,
                    IsAssociated = true
                };
        }
        else
        {
            var associatedPortalIds = db.ZnodeBStoresUserPortalRoles.AsNoTracking()
                .Where(r => r.UserId == userId && r.BStorePortalId != null)
                .Select(r => r.BStorePortalId!.Value);

            rowQuery =
                from p in db.ZnodePortals.AsNoTracking()
                where p.IsBStore && !associatedPortalIds.Contains(p.PortalId)
                orderby p.StoreName
                select new BStoreUserAccessRow
                {
                    PortalId     = p.PortalId,
                    StoreName    = p.StoreName ?? "",
                    IsActive     = p.IsActive,
                    IsAssociated = false
                };
        }

        var total = await rowQuery.CountAsync(ct);
        var items = await rowQuery
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new BStoreUserAccessListResult
        {
            Items      = items,
            TotalCount = total,
            PageIndex  = pageIndex,
            PageSize   = pageSize
        };
    }

    public async Task<bool> SaveUserRoleAccessAsync(BStoreUserRoleInput input, CancellationToken ct)
    {
        var statusParam = new SqlParameter("@Status", SqlDbType.Bit)
        {
            Direction = ParameterDirection.Output
        };

        await db.Database.ExecuteSqlRawAsync(
            """
            EXEC Znode_InsertBStoresAspNetUserRoles
                @UserId,
                @IsOwner,
                @IsManager,
                @CreatedBy,
                @Status OUTPUT
            """,
            ct,
            new SqlParameter("@UserId", input.UserId),
            new SqlParameter("@IsOwner", input.IsOwner),
            new SqlParameter("@IsManager", input.IsManager),
            new SqlParameter("@CreatedBy", 1),
            statusParam);

        if (statusParam.Value is bool b)
            return b;
        if (statusParam.Value is DBNull or null)
            return false;
        return Convert.ToBoolean(statusParam.Value);
    }

    public async Task<bool> ToggleUserAccessAsync(BStoreUserAccessInput input, CancellationToken ct)
    {
        if (input.PortalIds.Count == 0)
            return false;

        if (input.IsAssociate)
        {
            var aspRoleId = await db.AspNetRoles.AsNoTracking()
                .Where(r => r.TypeOfRole == "Customer" && r.Name == "Customer")
                .Select(r => r.Id)
                .FirstOrDefaultAsync(ct);
            if (aspRoleId is null)
            {
                log.LogWarning("ToggleUserAccessAsync: default Customer AspNet role not found.");
                return false;
            }

            var portalIds = input.PortalIds.Distinct().ToList();
            var validPortalIds = await db.ZnodePortals.AsNoTracking()
                .Where(p => portalIds.Contains(p.PortalId))
                .Select(p => p.PortalId)
                .ToListAsync(ct);
            if (validPortalIds.Count != portalIds.Count)
                return false;

            var existing = await db.ZnodeBStoresUserPortalRoles
                .Where(r => r.UserId == input.UserId && r.BStorePortalId != null && portalIds.Contains(r.BStorePortalId.Value))
                .Select(r => r.BStorePortalId!.Value)
                .ToListAsync(ct);

            var now = DateTime.UtcNow;
            foreach (var pid in portalIds.Where(pid => !existing.Contains(pid)))
            {
                db.ZnodeBStoresUserPortalRoles.Add(new ZnodeBStoresUserPortalRole
                {
                    UserId          = input.UserId,
                    BStorePortalId  = pid,
                    RoleId           = aspRoleId,
                    CreatedBy        = 1,
                    ModifiedBy       = 1,
                    CreatedDate      = now,
                    ModifiedDate     = now
                });
            }

            await db.SaveChangesAsync(ct);
            return true;
        }

        var toRemove = await db.ZnodeBStoresUserPortalRoles
            .Where(r => r.UserId == input.UserId
                        && r.BStorePortalId != null
                        && input.PortalIds.Contains(r.BStorePortalId.Value))
            .ToListAsync(ct);
        if (toRemove.Count == 0)
            return false;

        db.ZnodeBStoresUserPortalRoles.RemoveRange(toRemove);
        await db.SaveChangesAsync(ct);
        return true;
    }
}
