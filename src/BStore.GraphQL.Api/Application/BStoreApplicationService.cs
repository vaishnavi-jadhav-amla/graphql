using BStore.GraphQL.Api.Data;
using BStore.GraphQL.Api.GraphQL.Types;
using HotChocolate;
using HotChocolate.Types;
using ErrorCodes = BStore.GraphQL.Api.Common.ErrorCodes;

namespace BStore.GraphQL.Api.Application;

/// <summary>
/// B-store façade: reads and supported writes use <see cref="Znode.Libraries.Data.ZnodeEntity.Znode_Entities"/> via EF only.
/// Create, duplicate, and file operations are not implemented in-process (full Znode pipeline required).
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
        throw CreateNotSupported();

    public Task<bool> DuplicateStoreAsync(
        int sourcePortalId, int userId, DuplicateBStoreInput input, CancellationToken ct = default) =>
        throw new GraphQLException(ErrorBuilder.New()
            .SetMessage(
                "B-store copy is not implemented in the GraphQL database layer. Use Znode BStoresService or administrative tooling.")
            .SetCode(ErrorCodes.NotSupportedDbOperation)
            .Build());

    public Task<bool> SetActivationAsync(int storeId, int userId, bool active, CancellationToken ct = default) =>
        writes.SetActivationAsync(storeId, userId, active, ct);

    public Task<bool> UpdateStoreAsync(
        int storeId, int userId, UpdateBStoreSettingsInput input, CancellationToken ct = default) =>
        writes.UpdateStoreAsync(storeId, userId, input, ct);

    public Task<bool> UpdateStoreDesignAsync(
        int storeId, int userId, UpdateBStoreDesignInput input, CancellationToken ct = default) =>
        writes.UpdateStoreDesignAsync(storeId, userId, input, ct);

    public Task<FileUploadResult?> UploadFileAsync(IFile file, int mediaId, string? fileType, CancellationToken ct = default) =>
        throw FileNotSupported();

    public Task<bool> DeleteFileAsync(string mediaIds, CancellationToken ct = default) =>
        throw new GraphQLException(ErrorBuilder.New()
            .SetMessage(
                "File removal is not implemented in the GraphQL database layer. Use Znode media services or administrative tooling.")
            .SetCode(ErrorCodes.NotSupportedDbOperation)
            .Build());

    private static GraphQLException CreateNotSupported() =>
        new(ErrorBuilder.New()
            .SetMessage(
                "B-store creation is not implemented in the GraphQL database layer. Use Znode BStoresService or administrative tooling.")
            .SetCode(ErrorCodes.NotSupportedDbOperation)
            .Build());

    private static GraphQLException FileNotSupported() =>
        new(ErrorBuilder.New()
            .SetMessage(
                "File upload is not implemented in the GraphQL database layer. Use Znode media services or storage integration.")
            .SetCode(ErrorCodes.NotSupportedDbOperation)
            .Build());
}
