using BStore.GraphQL.Api.GraphQL.Types;

namespace BStore.GraphQL.Api.Data;

/// <summary>
/// B-store mutations that update <c>Znode_Entities</c> directly (no HTTP).
/// </summary>
public interface IBStoreEfWriteService
{
    Task<bool> SetActivationAsync(int storeId, int userId, bool active, CancellationToken ct = default);

    Task<bool> UpdateStoreAsync(
        int storeId, int userId, UpdateBStoreSettingsInput input, CancellationToken ct = default);

    Task<bool> UpdateStoreDesignAsync(
        int storeId, int userId, UpdateBStoreDesignInput input, CancellationToken ct = default);
}
