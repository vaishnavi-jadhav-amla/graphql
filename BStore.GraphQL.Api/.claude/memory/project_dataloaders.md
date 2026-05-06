---
name: DataLoader Pattern
description: Mandatory before production — solves N+1 query problem for nested GraphQL resolvers
type: project
---

## Why DataLoaders Are Non-Negotiable

Without DataLoaders:
- `products { reviews { author } }` = N×M DB queries per request
- At 500 req/sec with depth-3 queries = thousands of DB calls/sec
- Connection pool exhausted → cascading failures

## HotChocolate DataLoader Types

| Type | Use when | Example |
|---|---|---|
| `BatchDataLoader<TKey, TValue>` | Load single item per key, batch keys | Load product by id |
| `GroupedDataLoader<TKey, TValue>` | Load multiple items per key, batch keys | Load reviews by productId |
| `CacheDataLoader<TKey, TValue>` | Deduplicate within request | Same product requested by two fields |

## Standard Pattern (Copy for Every Domain)

```csharp
// DataLoaders/PIM/ProductDataLoader.cs
public class ProductByIdDataLoader : BatchDataLoader<int, ProductType?>
{
    private readonly IDbContextFactory<ZnodePublish_Entities> _dbFactory;

    public ProductByIdDataLoader(
        IDbContextFactory<ZnodePublish_Entities> dbFactory,
        IBatchScheduler scheduler,
        DataLoaderOptions? options = null)
        : base(scheduler, options)
    {
        _dbFactory = dbFactory;
    }

    protected override async Task<IReadOnlyDictionary<int, ProductType?>> LoadBatchAsync(
        IReadOnlyList<int> keys,
        CancellationToken ct)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var products = await db.ZnodePublishProductEntities
            .AsNoTracking()
            .Where(p => keys.Contains(p.ZnodeProductId))
            .ToListAsync(ct);

        return products.ToDictionary(
            p => p.ZnodeProductId,
            p => MapToProductType(p)  // same mapping as service
        );
    }
}
```

## Registration (in GraphQLServiceRegistration.cs)

```csharp
// DataLoaders register themselves automatically when injected into resolvers
// No explicit registration needed — HotChocolate handles DI scope
```

## Using in a Resolver

```csharp
public async Task<ProductType?> GetProduct(
    int productId,
    ProductByIdDataLoader loader,  // inject directly — no [Service] attribute
    CancellationToken ct)
    => await loader.LoadAsync(productId, ct);
```

## Pending DataLoaders to Implement

- [ ] `ProductByIdDataLoader` — batch by productId
- [ ] `CategoryByIdDataLoader` — batch by categoryId
- [ ] `ReviewsByProductDataLoader` — grouped by productId
- [ ] `AttributesByProductDataLoader` — grouped by productId
- [ ] `InventoryBySkuDataLoader` — batch by SKU (calls ProviderRegistry)
- [ ] `PriceBySkuDataLoader` — batch by SKU (calls ProviderRegistry)

## DbContextFactory Requirement

DataLoaders run outside the request scope. Switch from `DbContext` to `IDbContextFactory`:

```csharp
// In GraphQLServiceRegistration.RegisterServices():
builder.Services.AddDbContextFactory<ZnodePublish_Entities>(options =>
    options.UseSqlServer(connectionString));
```
