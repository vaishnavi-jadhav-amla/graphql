using BStore.GraphQL.Api.Caching;
using BStore.GraphQL.Api.Common;
using BStore.GraphQL.Api.Configuration;
using BStore.GraphQL.Api.Data;
using BStore.GraphQL.Api.Diagnostics;
using BStore.GraphQL.Api.GraphQL.Queries;
using BStore.GraphQL.Api.GraphQL.Types;
using HotChocolate;
using HotChocolate.Types;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ICacheService = BStore.GraphQL.Api.Caching.ICacheService;
using ErrorCodes = BStore.GraphQL.Api.Common.ErrorCodes;

namespace BStore.GraphQL.Api.GraphQL.Resolvers;

/// <summary>Product catalogue query fields (type extension on <see cref="BStoreQuery"/>).</summary>
[ExtendObjectType(typeof(BStoreQuery))]
public sealed class ProductQueryResolvers(
    ILogger<ProductQueryResolvers> logger,
    ICacheService cache,
    IOptions<GraphQLOptions> options,
    IRequestDebugContext debug,
    IEnumerable<IEmptyResultDiagnoser> diagnosers)
{
    private readonly GraphQLOptions _opts = options.Value;

    [GraphQLDescription("Paginated list from published products (Znode_Entities).")]
    public async Task<ProductListResult?> ProductList(
        [DefaultValue(24)] int limit,
        [DefaultValue(0)] int skip,
        string? sortBy,
        [DefaultValue("asc")] string order,
        string? select,
        [Service] IProductCatalogReadService catalog,
        CancellationToken ct)
    {
        limit = ClampPageSize(limit);
        debug.RecordDataSource(DataSource.ZnodeEntities);
        debug.Note("product.list", $"limit={limit} skip={skip} sortBy={sortBy} order={order}");
        logger.LogInformation(
            "ProductList | CorrelationId={CorrelationId} | limit={Limit} skip={Skip} sortBy={SortBy}",
            debug.CorrelationId, limit, skip, sortBy);

        try
        {
            using var _ = debug.Stage("product.list.cacheGetOrSet");
            var result = await cache.GetOrSetAsync<ProductListResult>(
                CacheKeys.ProductList(limit, skip, sortBy, order, select),
                () => catalog.GetProductsAsync(limit, skip, sortBy, order, select, ct),
                TimeSpan.FromSeconds(_opts.LookupCacheExpirySeconds),
                ct);

            if (result is null || result.Products.Count == 0)
                await DiagnoseEmpty("productList", new Dictionary<string, object?>
                {
                    ["limit"] = limit, ["skip"] = skip, ["sortBy"] = sortBy
                }, ct);

            return result;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "ProductList failed | CorrelationId={CorrelationId}", debug.CorrelationId);
            throw ErrorMapper.ToGraphQL(ex, ErrorCodes.BStoreError, debug);
        }
    }

    [GraphQLDescription("Single published product by publish product id.")]
    public async Task<ProductRow?> Product(
        int id,
        [Service] IProductCatalogReadService catalog,
        CancellationToken ct)
    {
        debug.RecordDataSource(DataSource.ZnodeEntities);
        debug.Note("product.byId", $"id={id}");
        logger.LogInformation("Product | CorrelationId={CorrelationId} | id={ProductId}", debug.CorrelationId, id);

        try
        {
            return await cache.GetOrSetAsync<ProductRow>(
                CacheKeys.ProductBase(id),
                () => catalog.GetProductByIdAsync(id, ct),
                TimeSpan.FromSeconds(_opts.DefaultCacheExpirySeconds),
                ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Product failed | CorrelationId={CorrelationId} | id={ProductId}", debug.CorrelationId, id);
            throw ErrorMapper.ToGraphQL(ex, ErrorCodes.BStoreError, debug);
        }
    }

    [GraphQLDescription("Search published products by name or SKU.")]
    public async Task<ProductListResult?> ProductSearch(
        string q,
        [DefaultValue(24)] int limit,
        [DefaultValue(0)] int skip,
        [Service] IProductCatalogReadService catalog,
        CancellationToken ct)
    {
        limit = ClampPageSize(limit);
        debug.RecordDataSource(DataSource.ZnodeEntities);
        debug.Note("product.search", $"q={q} limit={limit} skip={skip}");
        logger.LogInformation("ProductSearch | CorrelationId={CorrelationId} | q={Query} | limit={Limit}",
            debug.CorrelationId, q, limit);

        try
        {
            var result = await cache.GetOrSetAsync<ProductListResult>(
                CacheKeys.ProductSearch(q, limit, skip),
                () => catalog.SearchProductsAsync(q, limit, skip, ct),
                TimeSpan.FromSeconds(_opts.ListCacheExpirySeconds),
                ct);

            if (result is null || result.Products.Count == 0)
                await DiagnoseEmpty("productList", new Dictionary<string, object?>
                {
                    ["q"] = q, ["limit"] = limit, ["skip"] = skip
                }, ct);

            return result;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "ProductSearch failed | CorrelationId={CorrelationId} | q={Query}", debug.CorrelationId, q);
            throw ErrorMapper.ToGraphQL(ex, ErrorCodes.BStoreError, debug);
        }
    }

    [GraphQLDescription("Published category codes (ZnodePublishCategoryDetail).")]
    public async Task<List<ProductCategoryRow>?> ProductCategories(
        [Service] IProductCatalogReadService catalog,
        CancellationToken ct)
    {
        debug.RecordDataSource(DataSource.ZnodeEntities);
        try
        {
            return await cache.GetOrSetAsync<List<ProductCategoryRow>>(
                CacheKeys.ProductCategories(),
                () => catalog.GetCategoriesAsync(ct),
                TimeSpan.FromSeconds(_opts.LookupCacheExpirySeconds),
                ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "ProductCategories failed | CorrelationId={CorrelationId}", debug.CorrelationId);
            throw ErrorMapper.ToGraphQL(ex, ErrorCodes.BStoreError, debug);
        }
    }

    [GraphQLDescription("Published products in a category (by category code / slug).")]
    public async Task<ProductListResult?> ProductsByCategory(
        string category,
        [DefaultValue(24)] int limit,
        [DefaultValue(0)] int skip,
        [Service] IProductCatalogReadService catalog,
        CancellationToken ct)
    {
        limit = ClampPageSize(limit);
        debug.RecordDataSource(DataSource.ZnodeEntities);
        debug.Note("product.byCategory", $"category={category} limit={limit} skip={skip}");
        logger.LogInformation("ProductsByCategory | CorrelationId={CorrelationId} | category={Category}",
            debug.CorrelationId, category);

        try
        {
            var result = await cache.GetOrSetAsync<ProductListResult>(
                CacheKeys.ProductsByCategory(category, limit, skip),
                () => catalog.GetProductsByCategoryAsync(category, limit, skip, ct),
                TimeSpan.FromSeconds(_opts.LookupCacheExpirySeconds),
                ct);

            if (result is null || result.Products.Count == 0)
                await DiagnoseEmpty("productList", new Dictionary<string, object?>
                {
                    ["category"] = category, ["limit"] = limit, ["skip"] = skip
                }, ct);

            return result;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "ProductsByCategory failed | CorrelationId={CorrelationId} | category={Category}",
                debug.CorrelationId, category);
            throw ErrorMapper.ToGraphQL(ex, ErrorCodes.BStoreError, debug);
        }
    }

    private int ClampPageSize(int requested) =>
        Math.Clamp(requested <= 0 ? _opts.DefaultRelayPageSize : requested, 1, _opts.MaxPageSize);

    private async Task DiagnoseEmpty(string operation, IReadOnlyDictionary<string, object?> args, CancellationToken ct)
    {
        var d = diagnosers.FirstOrDefault(x => string.Equals(x.Operation, operation, StringComparison.OrdinalIgnoreCase));
        if (d is null) return;
        try
        {
            var reasons = await d.DiagnoseAsync(args, ct);
            foreach (var r in reasons) debug.RecordEmptyResultReason(r.Code, r.Message);
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Empty-result diagnoser failed for {Operation}", operation);
        }
    }
}
