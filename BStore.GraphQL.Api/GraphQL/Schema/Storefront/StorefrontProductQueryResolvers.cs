using BStore.GraphQL.Api.Caching;
using BStore.GraphQL.Api.Data;
using BStore.GraphQL.Api.Diagnostics;
using BStore.GraphQL.Api.GraphQL.Types;
using HotChocolate;
using HotChocolate.Types;
using ICacheService = BStore.GraphQL.Api.Caching.ICacheService;

namespace BStore.GraphQL.Api.GraphQL.Schema.Storefront;

/// <summary>
/// Storefront product queries — public-facing, customer-centric product operations.
/// Delegates to the same data services used by the unified schema.
/// </summary>
[ExtendObjectType(typeof(StorefrontQuery))]
public sealed class StorefrontProductQueryResolvers
{
    [GraphQLDescription("Get a product by id (storefront).")]
    public async Task<ProductRow?> Product(
        int productId,
        [Service] IProductCatalogReadService products,
        [Service] ICacheService cache,
        [Service] IRequestDebugContext debug,
        CancellationToken ct)
    {
        debug.RecordDataSource(DataSource.ZnodeEntities);
        return await cache.GetOrSetAsync(
            CacheKeys.Product(productId),
            () => products.GetProductByIdAsync(productId, ct),
            TimeSpan.FromSeconds(120), ct);
    }

    [GraphQLDescription("Search products by keyword (storefront).")]
    public async Task<ProductListResult?> ProductSearch(
        string query,
        int limit,
        int skip,
        [Service] IProductCatalogReadService products,
        [Service] IRequestDebugContext debug,
        CancellationToken ct)
    {
        debug.RecordDataSource(DataSource.ZnodeEntities);
        return await products.SearchProductsAsync(query, limit, skip, ct);
    }
}
