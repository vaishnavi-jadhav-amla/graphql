using BStore.GraphQL.Api.Diagnostics;
using GreenDonut;
using Microsoft.EntityFrameworkCore;
using Znode.Libraries.Data.ZnodeEntity;

namespace BStore.GraphQL.Api.DataLoaders;

/// <summary>
/// Batches product attribute lookups. Returns key-value attribute pairs per product id.
/// Uses ZnodePublishProductDetail available fields (ProductName, SKU) as basic attributes.
/// Full attribute data comes from the EAV attribute tables.
/// </summary>
public sealed class ProductAttributeDataLoader(
    IDbContextFactory<Znode_Entities> dbFactory,
    IRequestDebugContext debug,
    IBatchScheduler batchScheduler,
    DataLoaderOptions options)
    : GroupedDataLoader<int, KeyValuePair<string, string>>(batchScheduler, options)
{
    protected override async Task<ILookup<int, KeyValuePair<string, string>>> LoadGroupedBatchAsync(
        IReadOnlyList<int> keys,
        CancellationToken cancellationToken)
    {
        debug.RecordDataSource(DataSource.ZnodeEntities);
        using var _ = debug.Stage("dataLoader.productAttributes");

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        var rows = await db.ZnodePublishProductDetails.AsNoTracking()
            .Where(d => d.PublishProductId != null && keys.Contains(d.PublishProductId.Value))
            .Select(d => new
            {
                ProductId = d.PublishProductId!.Value,
                Name = d.ProductName ?? "Unknown",
                Sku = d.SKU ?? ""
            })
            .Distinct()
            .ToListAsync(cancellationToken);

        return rows.ToLookup(
            r => r.ProductId,
            r => new KeyValuePair<string, string>("SKU", r.Sku));
    }
}
