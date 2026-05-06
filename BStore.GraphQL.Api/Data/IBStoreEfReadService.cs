using BStore.GraphQL.Api.GraphQL.Types;

namespace BStore.GraphQL.Api.Data;

/// <summary>
/// B-store read model backed by <c>Znode_Entities</c> (same database layer as znode10-api-migration).
/// </summary>
public interface IBStoreEfReadService
{
    Task<BStoreListResult?> GetStoresAsync(int parentPortalId, int userId, CancellationToken ct = default);

    Task<BStoreDetails?> GetStoreDetailsAsync(int storeId, CancellationToken ct = default);

    Task<BStoreDesign?> GetStoreDesignAsync(int storeId, CancellationToken ct = default);

    Task<List<CatalogItem>?> GetCatalogsAsync(
        int portalId, bool associated, int pageIndex, int pageSize, string? filter,
        CancellationToken ct = default);

    Task<List<PriceListItem>?> GetPriceListsAsync(
        int portalId, bool associated, int pageIndex, int pageSize,
        CancellationToken ct = default);

    Task<string?> GetDomainNameSuffixAsync(int parentPortalId, CancellationToken ct = default);

    Task<List<DomainListItem>?> GetDomainListAsync(int pageIndex, int pageSize, CancellationToken ct = default);
}
