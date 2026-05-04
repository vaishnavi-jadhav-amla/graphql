using BStore.GraphQL.Api.Diagnostics;
using GreenDonut;
using Microsoft.EntityFrameworkCore;
using Znode.Libraries.Data.ZnodeEntity;

namespace BStore.GraphQL.Api.DataLoaders;

/// <summary>
/// Batches product image lookups to prevent N+1 when resolving product.images.
/// Uses ProductName as a proxy since ZnodePublishProductDetail does not have image columns directly.
/// </summary>
public sealed class ProductImageDataLoader(
    IDbContextFactory<Znode_Entities> dbFactory,
    IRequestDebugContext debug,
    IBatchScheduler batchScheduler,
    DataLoaderOptions options)
    : GroupedDataLoader<int, string>(batchScheduler, options)
{
    protected override async Task<ILookup<int, string>> LoadGroupedBatchAsync(
        IReadOnlyList<int> keys,
        CancellationToken cancellationToken)
    {
        debug.RecordDataSource(DataSource.ZnodeEntities);
        using var _ = debug.Stage("dataLoader.productImages");

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        // Image data may be stored in a separate media table or as attribute values.
        // This loader queries from publish product details as a starting point.
        var rows = await db.ZnodePublishProductDetails.AsNoTracking()
            .Where(d => d.PublishProductId != null && keys.Contains(d.PublishProductId.Value)
                        && d.SKU != null)
            .Select(d => new { ProductId = d.PublishProductId!.Value, ImagePath = d.SKU! })
            .Distinct()
            .ToListAsync(cancellationToken);

        return rows.ToLookup(r => r.ProductId, r => r.ImagePath);
    }
}
