---
name: Field Projection & Selective Loading
description: Only load the fields the client requested. At 100K+ products with JSON attribute columns, loading full entities for every query kills throughput.
type: project
---

## Problem

Product rows have a `PublishProductJson` column that is 2-20 KB. Loading it on a list query with 100 products means 2 MB transferred per request just for JSON columns — most of which the client never reads.

## Solution: Selection-Aware Service Methods

Services accept a "projection hint" and only load what's needed.

### Approach A: `IResolverContext` in resolver (HotChocolate native)

```csharp
public async Task<List<ProductType>> GetProducts(
    int portalId,
    IResolverContext ctx,
    [Service] IProductService svc,
    CancellationToken ct)
{
    // Get the list of fields the client actually requested
    var requested = ctx.GetSelections(ctx.ObjectType).Select(s => s.Field.Name).ToHashSet();

    var options = new ProductLoadOptions
    {
        IncludeAttributes = requested.Contains("attributes"),
        IncludePricing   = requested.Contains("pricing"),
        IncludeInventory = requested.Contains("inventory"),
        IncludeMedia     = requested.Contains("media") || requested.Contains("images"),
        IncludeSeo       = requested.Contains("seo"),
        IncludeReviews   = requested.Contains("reviews"),
    };

    return await svc.GetProductsAsync(portalId, options, ct);
}
```

### Approach B: `[UseProjection]` (EF Core + HotChocolate)

HotChocolate can automatically generate `.Select(p => new { p.Id, p.Name })` from the GraphQL query:

```csharp
[UseProjection]
public IQueryable<ProductType> GetProducts([Service] ZnodePublish_Entities db)
    => db.ZnodePublishProductEntities.AsNoTracking()
        .Select(MapToProductType);    // static expression tree
```

**Limitation:** `[UseProjection]` works for simple property mappings but breaks with JSON column deserialization. Use **Approach A** for Znode because attributes come from the `PublishProductJson` column.

## Service Implementation Pattern

```csharp
public class ProductLoadOptions
{
    public bool IncludeAttributes { get; set; }
    public bool IncludePricing   { get; set; }
    public bool IncludeInventory { get; set; }
    public bool IncludeMedia     { get; set; }
    public bool IncludeSeo       { get; set; }
    public bool IncludeReviews   { get; set; }
}

public async Task<List<ProductType>> GetProductsAsync(
    int portalId, ProductLoadOptions opts, CancellationToken ct)
{
    await using var db = await _dbFactory.CreateDbContextAsync(ct);

    // Base projection — only columns always needed
    var baseQuery = db.ZnodePublishProductEntities
        .AsNoTracking()
        .Where(p => p.PortalId == portalId && p.IsActive);

    // Conditionally include expensive columns
    var projected = opts.IncludeAttributes
        ? baseQuery.Select(p => new ProductWithJson
          {
              ProductId = p.ZnodeProductId,
              Name = p.Name,
              Sku = p.Sku,
              SeoUrl = p.SeoUrl,
              ImageName = p.ImageName,
              PublishProductJson = p.PublishProductJson  // expensive
          })
        : baseQuery.Select(p => new ProductWithJson
          {
              ProductId = p.ZnodeProductId,
              Name = p.Name,
              Sku = p.Sku,
              SeoUrl = p.SeoUrl,
              ImageName = p.ImageName
              // PublishProductJson omitted — save bandwidth
          });

    var rows = await projected.ToListAsync(ct);
    var products = rows.Select(r => MapToProductType(r, opts)).ToList();

    // Enrich from providers only if requested
    if (opts.IncludePricing)
        await EnrichPricingAsync(products, ct);
    if (opts.IncludeInventory)
        await EnrichInventoryAsync(products, ct);

    return products;
}
```

## Rules

1. **Never load a JSON column unless the resolver needs its contents.** `PublishProductJson`, `CategoryJson`, `GlobalAttributeGroups` are all expensive.
2. **Never call external providers** (pricing, inventory, tax) unless the resolver needs those fields.
3. **Reviews and related collections** go through a DataLoader — never load inline on a list query.
4. **Check `IResolverContext.GetSelections()` in every list resolver.**

## Measuring Impact

Before/after for `getProducts(portalId: 1, first: 100)` without attributes:
- Without projection: 6-10 MB SQL payload, 400-600ms response
- With selection-aware projection: 50-100 KB SQL payload, 40-80ms response

**10x improvement for the common case of "list products without detail fields."**

## Audit Checklist (For Existing Services)

- [ ] `ProductService.GetProductsBySeoUrlAsync` — honors projection?
- [ ] `ProductService.SearchProductsAsync` — honors projection?
- [ ] `CategoryService.GetCategoriesAsync` — honors projection?
- [ ] `OrderService.GetOrderHistoryAsync` — omits line items unless requested?
- [ ] `CartService.GetCartAsync` — omits saved-for-later unless requested?
