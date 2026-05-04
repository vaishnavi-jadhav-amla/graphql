using BStore.GraphQL.Api.GraphQL.Types;
using HotChocolate.Types;

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

    /// <summary>Inserts a new B-store portal under <paramref name="parentPortalId"/>.</summary>
    Task<CreateStoreResult?> CreateStoreAsync(
        int parentPortalId, int userId, CreateBStoreInput input, CancellationToken ct = default);

    /// <summary>Duplicates a source portal: clones the row plus catalog and price-list assignments.</summary>
    Task<bool> DuplicateStoreAsync(
        int sourcePortalId, int userId, DuplicateBStoreInput input, CancellationToken ct = default);

    /// <summary>Persists a file to the configured upload folder and inserts a <c>ZnodeMedia</c> row.</summary>
    Task<FileUploadResult?> UploadFileAsync(IFile file, int mediaId, string? fileType, CancellationToken ct = default);

    /// <summary>Deletes media rows and underlying files for the comma-separated id list.</summary>
    Task<bool> DeleteFileAsync(string mediaIds, CancellationToken ct = default);
}
