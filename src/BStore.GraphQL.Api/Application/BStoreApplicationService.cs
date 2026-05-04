using BStore.GraphQL.Api.Data;
using BStore.GraphQL.Api.GraphQL.Types;
using HotChocolate.Types;

namespace BStore.GraphQL.Api.Application;

/// <summary>
/// B-store façade: reads and supported writes use <see cref="Znode.Libraries.Data.ZnodeEntity.Znode_Entities"/> via EF only.
/// All mutations delegate to <see cref="IBStoreEfWriteService"/>.
/// </summary>
public sealed class BStoreApplicationService(IBStoreEfReadService reads, IBStoreEfWriteService writes)
    : IBStoreApplicationService
{
    private const int DomainListDefaultPageSize = 500;

    public Task<BStoreListResult?> GetStoresAsync(int portalId, int userId, CancellationToken ct = default) =>
        reads.GetStoresAsync(portalId, userId, ct);

    public Task<BStoreDetails?> GetStoreDetailsAsync(int storeId, CancellationToken ct = default) =>
        reads.GetStoreDetailsAsync(storeId, ct);

    public Task<BStoreDesign?> GetStoreDesignAsync(int storeId, CancellationToken ct = default) =>
        reads.GetStoreDesignAsync(storeId, ct);

    public Task<List<CatalogItem>?> GetCatalogsAsync(
        int portalId, bool associated, int pageIndex, int pageSize, string? filter, CancellationToken ct = default) =>
        reads.GetCatalogsAsync(portalId, associated, pageIndex, pageSize, filter, ct);

    public Task<List<PriceListItem>?> GetPriceListsAsync(
        int portalId, bool associated, int pageIndex, int pageSize, CancellationToken ct = default) =>
        reads.GetPriceListsAsync(portalId, associated, pageIndex, pageSize, ct);

    public Task<string?> GetDomainNameSuffixAsync(int portalId, CancellationToken ct = default) =>
        reads.GetDomainNameSuffixAsync(portalId, ct);

    public Task<List<DomainListItem>?> GetDomainListAsync(CancellationToken ct = default) =>
        reads.GetDomainListAsync(1, DomainListDefaultPageSize, ct);

    public Task<CreateStoreResult?> CreateStoreAsync(
        int portalId, int userId, CreateBStoreInput input, CancellationToken ct = default) =>
        writes.CreateStoreAsync(portalId, userId, input, ct);

    public Task<bool> DuplicateStoreAsync(
        int sourcePortalId, int userId, DuplicateBStoreInput input, CancellationToken ct = default) =>
        writes.DuplicateStoreAsync(sourcePortalId, userId, input, ct);

    public Task<bool> SetActivationAsync(int storeId, int userId, bool active, CancellationToken ct = default) =>
        writes.SetActivationAsync(storeId, userId, active, ct);

    public Task<bool> UpdateStoreAsync(
        int storeId, int userId, UpdateBStoreSettingsInput input, CancellationToken ct = default) =>
        writes.UpdateStoreAsync(storeId, userId, input, ct);

    public Task<bool> UpdateStoreDesignAsync(
        int storeId, int userId, UpdateBStoreDesignInput input, CancellationToken ct = default) =>
        writes.UpdateStoreDesignAsync(storeId, userId, input, ct);

    public Task<FileUploadResult?> UploadFileAsync(IFile file, int mediaId, string? fileType, CancellationToken ct = default) =>
        writes.UploadFileAsync(file, mediaId, fileType, ct);

    public Task<bool> DeleteFileAsync(string mediaIds, CancellationToken ct = default) =>
        writes.DeleteFileAsync(mediaIds, ct);
}
