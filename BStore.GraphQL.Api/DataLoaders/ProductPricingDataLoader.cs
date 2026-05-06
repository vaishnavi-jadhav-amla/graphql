using BStore.GraphQL.Api.Diagnostics;
using GreenDonut;
using Microsoft.EntityFrameworkCore;
using Znode.Libraries.Data.ZnodeEntity;

namespace BStore.GraphQL.Api.DataLoaders;

/// <summary>
/// Batches product pricing lookups. Returns product id → exists mapping.
/// Actual pricing data comes from the ZnodePublishProductPrice table or external providers.
/// Short-TTL data — should be combined with cache TTL of &lt;= 30 seconds.
/// </summary>
public sealed class ProductPricingDataLoader(
    IDbContextFactory<Znode_Entities> dbFactory,
    IRequestDebugContext debug,
    IBatchScheduler batchScheduler,
    DataLoaderOptions options)
    : BatchDataLoader<int, decimal?>(batchScheduler, options)
{
    protected override async Task<IReadOnlyDictionary<int, decimal?>> LoadBatchAsync(
        IReadOnlyList<int> keys,
        CancellationToken cancellationToken)
    {
        debug.RecordDataSource(DataSource.ZnodeEntities);
        using var _ = debug.Stage("dataLoader.productPricing");

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        // Pricing may come from ZnodePublishProductPrice or external providers.
        // As a baseline, query from publish product details.
        var rows = await db.ZnodePublishProductDetails.AsNoTracking()
            .Where(d => d.PublishProductId != null && keys.Contains(d.PublishProductId.Value))
            .GroupBy(d => d.PublishProductId!.Value)
            .Select(g => new { ProductId = g.Key })
            .ToListAsync(cancellationToken);

        return rows.ToDictionary(
            r => r.ProductId,
            _ => (decimal?)0m);
    }
}
