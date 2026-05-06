using BStore.GraphQL.Api.GraphQL.Types;

namespace BStore.GraphQL.Api.Data;

/// <summary>B-store user role and portal access: reads/writes <c>Znode_Entities</c> (no HTTP).</summary>
public interface IBStoreUserDataService
{
    Task<BStoreUserRoleRow?> GetUserRoleAccessAsync(int userId, CancellationToken ct = default);

    Task<BStoreUserAccessListResult?> GetUserAccessListAsync(
        int userId, bool isAssociated, int pageIndex, int pageSize, CancellationToken ct = default);

    Task<bool> SaveUserRoleAccessAsync(BStoreUserRoleInput input, CancellationToken ct = default);

    Task<bool> ToggleUserAccessAsync(BStoreUserAccessInput input, CancellationToken ct = default);
}
