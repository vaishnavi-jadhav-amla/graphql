using BStore.GraphQL.Api.Diagnostics;
using GreenDonut;
using Microsoft.EntityFrameworkCore;
using Znode.Libraries.Data.ZnodeEntity;

namespace BStore.GraphQL.Api.DataLoaders;

/// <summary>
/// Batches product inventory lookups. Returns product id → existence check.
/// Actual inventory quantities come from ZnodeInventory or external WMS providers.
/// Short-TTL data — should use cache TTL of &lt;= 30 seconds.
/// </summary>
public sealed class ProductInventoryDataLoader(
    IDbContextFactory<Znode_Entities> dbFactory,
    IRequestDebugContext debug,
    IBatchScheduler batchScheduler,
    DataLoaderOptions options)
    : BatchDataLoader<int, int?>(batchScheduler, options)
{
    protected override async Task<IReadOnlyDictionary<int, int?>> LoadBatchAsync(
        IReadOnlyList<int> keys,
        CancellationToken cancellationToken)
    {
        debug.RecordDataSource(DataSource.ZnodeEntities);
        using var _ = debug.Stage("dataLoader.productInventory");

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        // Inventory data may come from ZnodeInventory table or external providers.
        // As a baseline, check product existence in publish details.
        var rows = await db.ZnodePublishProductDetails.AsNoTracking()
            .Where(d => d.PublishProductId != null && keys.Contains(d.PublishProductId.Value))
            .GroupBy(d => d.PublishProductId!.Value)
            .Select(g => new { ProductId = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        return rows.ToDictionary(
            r => r.ProductId,
            r => (int?)r.Count);
    }
}
