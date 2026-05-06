---
paths:
  - "**/DataLoaders/**/*.cs"
  - "**/Services/**/*.cs"
  - "**/Queries/**/*.cs"
---

# DataLoader Rules (ADR-004 — Mandatory N+1 Prevention)

## When a DataLoader Is Required

Any resolver that loads related data **inside a list result** must use a DataLoader. Direct DB calls inside nested resolvers are **forbidden**.

| Scenario | DataLoader type |
|---|---|
| Load one entity by ID, called many times | `BatchDataLoader<TKey, TResult>` |
| Load a list of entities by a parent ID | `GroupedDataLoader<TKey, TResult>` |
| Products → prices (1:1 per SKU) | `BatchDataLoader<string, PriceInfo>` |
| Categories → products (1:N) | `GroupedDataLoader<int, ProductType>` |
| Products → inventory per SKU | `BatchDataLoader<string, InventoryInfo>` |

## File Location

All DataLoaders live in `DataLoaders/{Domain}/`:
- `DataLoaders/PIM/ProductByIdDataLoader.cs`
- `DataLoaders/PIM/PriceBySkuDataLoader.cs`
- `DataLoaders/PIM/InventoryBySkuDataLoader.cs`
- `DataLoaders/OMS/OrderByIdDataLoader.cs`

## BatchDataLoader Pattern

```csharp
// DataLoaders/PIM/ProductByIdDataLoader.cs
public class ProductByIdDataLoader : BatchDataLoader<int, ProductType?>
{
    private readonly IDbContextFactory<ZnodePublish_Entities> _dbFactory;

    public ProductByIdDataLoader(
        IDbContextFactory<ZnodePublish_Entities> dbFactory,
        IBatchScheduler scheduler,
        DataLoaderOptions options)
        : base(scheduler, options)
    {
        _dbFactory = dbFactory;
    }

    protected override async Task<IReadOnlyDictionary<int, ProductType?>> LoadBatchAsync(
        IReadOnlyList<int> keys,
        CancellationToken ct)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var entities = await db.ZnodePublishProductEntities
            .AsNoTracking()
            .Where(p => keys.Contains(p.ZnodeProductId))
            .Select(p => new { p.ZnodeProductId, p.Name, p.Sku })
            .ToListAsync(ct);

        return entities.ToDictionary(
            e => e.ZnodeProductId,
            e => (ProductType?)new ProductType { ProductId = e.ZnodeProductId, Name = e.Name, Sku = e.Sku });
    }
}
```

## GroupedDataLoader Pattern

```csharp
// DataLoaders/PIM/ProductsByCategoryDataLoader.cs
public class ProductsByCategoryDataLoader : GroupedDataLoader<int, ProductType>
{
    private readonly IDbContextFactory<ZnodePublish_Entities> _dbFactory;

    public ProductsByCategoryDataLoader(
        IDbContextFactory<ZnodePublish_Entities> dbFactory,
        IBatchScheduler scheduler,
        DataLoaderOptions options)
        : base(scheduler, options)
    {
        _dbFactory = dbFactory;
    }

    protected override async Task<ILookup<int, ProductType>> LoadGroupedBatchAsync(
        IReadOnlyList<int> categoryIds,
        CancellationToken ct)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var rows = await db.ZnodePublishCategoryProductEntities
            .AsNoTracking()
            .Where(cp => categoryIds.Contains(cp.CategoryId))
            .Select(cp => new { cp.CategoryId, cp.ZnodeProductId, cp.DisplayOrder })
            .ToListAsync(ct);

        // Return as lookup: categoryId → [products]
        return rows.ToLookup(r => r.CategoryId, r => new ProductType { ProductId = r.ZnodeProductId });
    }
}
```

## Using DataLoaders in Resolvers

```csharp
// In a Query or nested resolver:
public Task<ProductType?> GetProduct(
    int productId,
    ProductByIdDataLoader loader,   // ← inject by type (HotChocolate registers automatically)
    CancellationToken ct)
    => loader.LoadAsync(productId, ct);
```

## Registration

DataLoaders are **auto-registered by HotChocolate** when discovered. Add the DataLoaders assembly to HC in `GraphQLServiceRegistration.cs`:

```csharp
// RegisterHotChocolate:
.AddDataLoader<ProductByIdDataLoader>()
.AddDataLoader<PriceBySkuDataLoader>()
// ... or use AddDataLoaders() to scan the assembly
```

Also register `IDbContextFactory<ZnodePublish_Entities>` (required by DataLoaders — they create their own scope per batch):

```csharp
builder.Services.AddDbContextFactory<ZnodePublish_Entities>(options =>
    options.UseSqlServer(connectionString));
```

## Per-Batch Exception Handling

DataLoader `LoadBatchAsync` must not throw — a single failed entity should not fail the whole batch:

```csharp
protected override async Task<IReadOnlyDictionary<int, ProductType?>> LoadBatchAsync(
    IReadOnlyList<int> keys, CancellationToken ct)
{
    try
    {
        // ... load all keys
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "DataLoader batch failed | Keys:{Count}", keys.Count);
        // Return nulls for all keys — resolver gets null, not an error
        return keys.ToDictionary(k => k, _ => (ProductType?)null);
    }
}
```

## Forbidden Patterns

```csharp
// ❌ FORBIDDEN — N+1: called once per product in a list
public async Task<PriceInfo> GetPrice(int productId, [Service] IPriceService svc)
    => await svc.GetPriceAsync(productId);  // 100 products = 100 DB calls

// ✅ REQUIRED — batched: all 100 products → 1 DB call
public Task<PriceInfo?> GetPrice(int productId, PriceBySkuDataLoader loader, CancellationToken ct)
    => loader.LoadAsync(productId, ct);
```
