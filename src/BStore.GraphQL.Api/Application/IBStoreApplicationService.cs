using BStore.GraphQL.Api.GraphQL.Types;
using HotChocolate.Types;

namespace BStore.GraphQL.Api.Application;

/// <summary>
/// Application-level B-store operations. GraphQL resolvers use this contract; all B-store data access
/// is implemented with Entity Framework against <c>Znode_Entities</c> (no HTTP client for B-store).
/// </summary>
public interface IBStoreApplicationService
{
    /// <summary>B-store list for a user under a parent portal (EF).</summary>
    Task<BStoreListResult?> GetStoresAsync(int portalId, int userId, CancellationToken ct = default);

    /// <summary>B-store details by portal id (EF).</summary>
    Task<BStoreDetails?> GetStoreDetailsAsync(int storeId, CancellationToken ct = default);

    /// <summary>B-store theme row (EF).</summary>
    Task<BStoreDesign?> GetStoreDesignAsync(int storeId, CancellationToken ct = default);

    /// <summary>Catalog assignments for a portal (EF).</summary>
    Task<List<CatalogItem>?> GetCatalogsAsync(
        int portalId, bool associated, int pageIndex, int pageSize, string? filter,
        CancellationToken ct = default);

    /// <summary>Price list assignments for a portal (EF).</summary>
    Task<List<PriceListItem>?> GetPriceListsAsync(
        int portalId, bool associated, int pageIndex, int pageSize,
        CancellationToken ct = default);

    /// <summary>Parent portal B-store domain suffix (EF).</summary>
    Task<string?> GetDomainNameSuffixAsync(int portalId, CancellationToken ct = default);

    /// <summary>Domain list (EF).</summary>
    Task<List<DomainListItem>?> GetDomainListAsync(CancellationToken ct = default);

    /// <summary>Creates a B-store (not implemented in-process; throws).</summary>
    Task<CreateStoreResult?> CreateStoreAsync(
        int portalId, int userId, CreateBStoreInput input, CancellationToken ct = default);

    /// <summary>Duplicates a B-store (not implemented in-process; throws).</summary>
    Task<bool> DuplicateStoreAsync(
        int sourcePortalId, int userId, DuplicateBStoreInput input, CancellationToken ct = default);

    /// <summary>Activates or deactivates a B-store (EF).</summary>
    Task<bool> SetActivationAsync(int storeId, int userId, bool active, CancellationToken ct = default);

    /// <summary>Updates core portal fields (EF).</summary>
    Task<bool> UpdateStoreAsync(
        int storeId, int userId, UpdateBStoreSettingsInput input, CancellationToken ct = default);

    /// <summary>Updates CMS portal theme (EF).</summary>
    Task<bool> UpdateStoreDesignAsync(
        int storeId, int userId, UpdateBStoreDesignInput input, CancellationToken ct = default);

    /// <summary>Uploads media (not implemented in-process; throws).</summary>
    Task<FileUploadResult?> UploadFileAsync(IFile file, int mediaId, string? fileType, CancellationToken ct = default);

    /// <summary>Removes media (not implemented in-process; throws).</summary>
    Task<bool> DeleteFileAsync(string mediaIds, CancellationToken ct = default);
}
