using BStore.GraphQL.Api.Application;
using BStore.GraphQL.Api.Auth.FieldPermissions;
using BStore.GraphQL.Api.Caching;
using BStore.GraphQL.Api.Common;
using BStore.GraphQL.Api.Configuration;
using BStore.GraphQL.Api.GraphQL.Queries;
using BStore.GraphQL.Api.GraphQL.Types;
using HotChocolate;
using HotChocolate.Types;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ICacheService = BStore.GraphQL.Api.Caching.ICacheService;

namespace BStore.GraphQL.Api.GraphQL.Resolvers;

/// <summary>
/// Field resolvers for B-store data, attached to <see cref="BStoreQuery"/> via GraphQL type extension.
/// </summary>
[ExtendObjectType(typeof(BStoreQuery))]
public sealed class BStoreQueryResolvers(
    ILogger<BStoreQueryResolvers> logger,
    ICacheService cache,
    IOptions<GraphQLOptions> options)
{
    private readonly GraphQLOptions _opts = options.Value;

    [GraphQLDescription("Downstream: GET /v2/b-stores/parent-portal/{portalId}/users/{userId}/stores")]
    [RequireAuthenticated]
    public async Task<BStoreListResult?> BStoreList(
        int portalId,
        int userId,
        [Service] IBStoreApplicationService bStore,
        CancellationToken ct)
    {
        logger.LogInformation("BStoreList: portalId={PortalId} userId={UserId}", portalId, userId);
        var key = CacheKeys.BStoreList(portalId, userId, 1, _opts.MaxPageSize);
        try
        {
            return await cache.GetOrSetAsync(key,
                () => bStore.GetStoresAsync(portalId, userId, ct),
                TimeSpan.FromSeconds(_opts.ListCacheExpirySeconds),
                ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "BStoreList failed");
            throw ErrorMapper.ToGraphQL(ex);
        }
    }

    [GraphQLDescription("Downstream: GET /v2/b-stores/{storeId}")]
    [RequireAuthenticated]
    public async Task<BStoreDetails?> BStore(
        int storeId,
        [Service] IBStoreApplicationService bStore,
        CancellationToken ct)
    {
        logger.LogInformation("BStore: storeId={StoreId}", storeId);
        var key = CacheKeys.BStore(storeId);
        try
        {
            return await cache.GetOrSetAsync(key,
                () => bStore.GetStoreDetailsAsync(storeId, ct),
                TimeSpan.FromSeconds(_opts.DefaultCacheExpirySeconds),
                ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "BStore failed: storeId={StoreId}", storeId);
            throw ErrorMapper.ToGraphQL(ex);
        }
    }

    [GraphQLDescription("Downstream: GET /v2/b-stores/{storeId}/theme")]
    [RequireAuthenticated]
    public async Task<BStoreDesign?> BStoreTheme(
        int storeId,
        [Service] IBStoreApplicationService bStore,
        CancellationToken ct)
    {
        logger.LogInformation("BStoreTheme: storeId={StoreId}", storeId);
        var key = CacheKeys.BStoreTheme(storeId);
        try
        {
            return await cache.GetOrSetAsync(key,
                () => bStore.GetStoreDesignAsync(storeId, ct),
                TimeSpan.FromSeconds(_opts.DefaultCacheExpirySeconds),
                ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "BStoreTheme failed: storeId={StoreId}", storeId);
            throw ErrorMapper.ToGraphQL(ex);
        }
    }

    [GraphQLDescription("Downstream: GET /v2/b-stores/parent-portal/{portalId}/catalogs")]
    [RequireAuthenticated]
    public async Task<List<CatalogItem>?> BStoreCatalogs(
        int portalId,
        bool associated,
        int pageIndex,
        int pageSize,
        string? filter,
        [Service] IBStoreApplicationService bStore,
        CancellationToken ct)
    {
        var effectiveFilter = filter
            ?? (associated ? BStoreHttpConstants.CatalogFilterManagedInBStore : null);
        logger.LogInformation("BStoreCatalogs: portalId={PortalId}", portalId);
        var key = CacheKeys.BStoreCatalogs(portalId, associated, pageIndex, pageSize, effectiveFilter);
        try
        {
            return await cache.GetOrSetAsync(key,
                () => bStore.GetCatalogsAsync(portalId, associated, pageIndex, pageSize, effectiveFilter, ct),
                TimeSpan.FromSeconds(_opts.LookupCacheExpirySeconds),
                ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "BStoreCatalogs failed: portalId={PortalId}", portalId);
            throw ErrorMapper.ToGraphQL(ex);
        }
    }

    [GraphQLDescription("Downstream: GET /v2/b-stores/parent-portal/{portalId}/price-list")]
    [RequireAuthenticated]
    public async Task<List<PriceListItem>?> BStorePriceLists(
        int portalId,
        bool associated,
        int pageIndex,
        int pageSize,
        [Service] IBStoreApplicationService bStore,
        CancellationToken ct)
    {
        logger.LogInformation("BStorePriceLists: portalId={PortalId}", portalId);
        var key = CacheKeys.BStorePriceLists(portalId, associated, pageIndex, pageSize);
        try
        {
            return await cache.GetOrSetAsync(key,
                () => bStore.GetPriceListsAsync(portalId, associated, pageIndex, pageSize, ct),
                TimeSpan.FromSeconds(_opts.LookupCacheExpirySeconds),
                ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "BStorePriceLists failed: portalId={PortalId}", portalId);
            throw ErrorMapper.ToGraphQL(ex);
        }
    }

    [GraphQLDescription("Downstream: GET /v2/b-stores/parent-portal/{portalId}/domain-name")]
    [RequireAuthenticated]
    public async Task<string?> BStoreDomainNameSuffix(
        int portalId,
        [Service] IBStoreApplicationService bStore,
        CancellationToken ct)
    {
        logger.LogInformation("BStoreDomainNameSuffix: portalId={PortalId}", portalId);
        var key = CacheKeys.BStoreDomainNameSuffix(portalId);
        try
        {
            return await cache.GetOrSetAsync(key,
                () => bStore.GetDomainNameSuffixAsync(portalId, ct),
                TimeSpan.FromSeconds(_opts.LookupCacheExpirySeconds),
                ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "BStoreDomainNameSuffix failed: portalId={PortalId}", portalId);
            throw ErrorMapper.ToGraphQL(ex);
        }
    }

    [GraphQLDescription("Downstream: GET /Domain/List")]
    [RequireAuthenticated]
    public async Task<List<DomainListItem>?> DomainList(
        [Service] IBStoreApplicationService bStore,
        CancellationToken ct)
    {
        logger.LogInformation("DomainList called");
        var key = CacheKeys.DomainList(1, _opts.MaxPageSize);
        try
        {
            return await cache.GetOrSetAsync(key,
                () => bStore.GetDomainListAsync(ct),
                TimeSpan.FromSeconds(_opts.LookupCacheExpirySeconds),
                ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "DomainList failed");
            throw ErrorMapper.ToGraphQL(ex);
        }
    }
}
