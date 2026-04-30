using BStore.GraphQL.Api.Diagnostics;
using BStore.GraphQL.Api.GraphQL.Types;
using GreenDonut;
using Microsoft.EntityFrameworkCore;
using Znode.Libraries.Data.ZnodeEntity;

namespace BStore.GraphQL.Api.DataLoaders;

/// <summary>
/// ADR-004: nested product look-ups must batch through <see cref="ProductByIdDataLoader"/>.
/// Resolvers / DataLoaders that traverse <c>order.lineItems[].product</c>, <c>cart.items[].product</c>,
/// or attribute references should call <see cref="LoadAsync"/> instead of issuing a query per id.
/// </summary>
public sealed class ProductByIdDataLoader(
    IDbContextFactory<Znode_Entities> dbFactory,
    IRequestDebugContext debug,
    IBatchScheduler batchScheduler,
    DataLoaderOptions options)
    : BatchDataLoader<int, ProductRow>(batchScheduler, options)
{
    protected override async Task<IReadOnlyDictionary<int, ProductRow>> LoadBatchAsync(
        IReadOnlyList<int> keys,
        CancellationToken cancellationToken)
    {
        debug.RecordDataSource(DataSource.ZnodeEntities);
        using var _ = debug.Stage("dataLoader.productById");

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        var rows = await db.ZnodePublishProductDetails.AsNoTracking()
            .Where(d => d.PublishProductId != null && keys.Contains(d.PublishProductId.Value))
            .GroupBy(d => d.PublishProductId!.Value)
            .Select(g => new
            {
                Id    = g.Key,
                Title = g.Min(x => x.ProductName) ?? "",
                Sku   = g.Min(x => x.SKU)
            })
            .ToListAsync(cancellationToken);

        return rows.ToDictionary(
            r => r.Id,
            r => new ProductRow
            {
                Id    = r.Id,
                Title = r.Title,
                Sku   = r.Sku,
                Description = "",
                Category    = "",
                Tags        = [],
                Images      = []
            });
    }
}
