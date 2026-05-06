using BStore.GraphQL.Api.GraphQL.Types;

namespace BStore.GraphQL.Api.Data;

/// <summary>Znode user reads/updates against <c>Znode_Entities</c> (no HTTP).</summary>
public interface IUserDataService
{
    Task<UserListPage?> GetUsersPagedAsync(int page, int pageSize, CancellationToken ct = default);

    Task<UserRow?> GetUserAsync(int userId, CancellationToken ct = default);

    Task<UserRow?> GetUserByUsernameAsync(string username, string storeCode, CancellationToken ct = default);

    Task<UserRow?> CreateUserAsync(UserCreateInput input, CancellationToken ct = default);

    Task<bool> UpdateUserAsync(UserUpdateInput input, CancellationToken ct = default);

    Task<bool> ToggleUserActiveAsync(int userId, bool lockUser, CancellationToken ct = default);

    Task<bool> DeleteUserAsync(string id, CancellationToken ct = default);
}
