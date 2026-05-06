using BStore.GraphQL.Api.Diagnostics;
using GreenDonut;
using Microsoft.EntityFrameworkCore;
using Znode.Libraries.Data.ZnodeEntity;

namespace BStore.GraphQL.Api.DataLoaders;

/// <summary>
/// Batches order line item lookups to prevent N+1 when resolving order.lineItems.
/// </summary>
public sealed class OrderLineItemDataLoader(
    IDbContextFactory<Znode_Entities> dbFactory,
    IRequestDebugContext debug,
    IBatchScheduler batchScheduler,
    DataLoaderOptions options)
    : GroupedDataLoader<int, OrderLineItemRow>(batchScheduler, options)
{
    protected override async Task<ILookup<int, OrderLineItemRow>> LoadGroupedBatchAsync(
        IReadOnlyList<int> keys,
        CancellationToken cancellationToken)
    {
        debug.RecordDataSource(DataSource.ZnodeEntities);
        using var _ = debug.Stage("dataLoader.orderLineItems");

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        var rows = await db.ZnodeOmsOrderLineItems.AsNoTracking()
            .Where(li => keys.Contains(li.OmsOrderDetailsId))
            .Select(li => new OrderLineItemRow
            {
                OmsOrderDetailsId = li.OmsOrderDetailsId,
                OrderLineItemId = li.OmsOrderLineItemsId,
                ProductName = li.ProductName ?? "",
                SKU = li.Sku ?? "",
                Quantity = (int)(li.Quantity ?? 0m),
                Price = li.Price,
                ExtendedPrice = li.Price * (li.Quantity ?? 0m)
            })
            .ToListAsync(cancellationToken);

        return rows.ToLookup(r => r.OmsOrderDetailsId);
    }
}

public sealed class OrderLineItemRow
{
    public int OmsOrderDetailsId { get; set; }
    public int OrderLineItemId { get; set; }
    public string ProductName { get; set; } = "";
    public string SKU { get; set; } = "";
    public int Quantity { get; set; }
    public decimal Price { get; set; }
    public decimal ExtendedPrice { get; set; }
}
