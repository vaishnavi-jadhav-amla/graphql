---
paths:
  - "**/Services/**/*.cs"
  - "**/Queries/**/*.cs"
  - "**/DataLoaders/**/*.cs"
---

# Big-Data Query Rules (100K-1M Row Scale)

## Pagination Rules (FORBIDDEN vs REQUIRED)

### Forbidden
- ❌ `pageSize`, `pageNumber` arguments → OFFSET-based, O(N) cost
- ❌ `.Skip(skip).Take(take)` where `skip > 0`
- ❌ Returning `List<T>` or `IEnumerable<T>` from a list resolver
- ❌ Unbounded sorts on non-unique fields (no tiebreaker)
- ❌ `totalCount` returned by default — must be opt-in

### Required
- ✅ `first: Int, after: String` cursor arguments (Relay spec)
- ✅ Return `Connection<T>` with `edges`, `pageInfo`
- ✅ Base64-encoded opaque cursor
- ✅ `.OrderBy(sortField).ThenBy(p => p.Id)` — total order
- ✅ `Take(first + 1)` to detect `hasNextPage`

## SQL Query Rules

### Forbidden
- ❌ `WHERE col LIKE '%...%'` on large tables → table scan
- ❌ `.Include(p => p.SomeCollection)` on list queries → cartesian + N+1
- ❌ `SaveChangesAsync` in a loop for >100 rows
- ❌ Querying EAV tables (`ZnodeProductAttribute*`) from storefront reads
- ❌ Loading entire `PublishProductJson` column on list queries without selection check
- ❌ Recursive CTE on category tree at runtime — use materialized `CategoryPath` instead

### Required
- ✅ `.AsNoTracking()` on every read query
- ✅ `.Select(new DtoType { ... })` projection — never return full entities
- ✅ Read from `ZnodePublish_Entities` only for storefront; `Znode_Entities` only for writes
- ✅ Every new query must be index-covered — validate against indexes in `project_data_scale.md`
- ✅ `SqlBulkCopy` for >100 row writes
- ✅ Connection string with `MaxPoolSize=200;MinPoolSize=20`

## Selection-Aware Loading

List resolvers that touch expensive columns MUST inspect the selection set:

```csharp
var requested = ctx.GetSelections(ctx.ObjectType).Select(s => s.Field.Name).ToHashSet();
var opts = new ProductLoadOptions
{
    IncludeAttributes = requested.Contains("attributes"),
    IncludePricing   = requested.Contains("pricing"),
    IncludeInventory = requested.Contains("inventory"),
};
return await svc.GetProductsAsync(portalId, opts, ct);
```

## Search vs SQL

| Use Search Service | Use SQL Directly |
|---|---|
| Full-text search on products, categories, pages | Single lookup by primary key |
| Faceted filtering (by attribute values) | Cart / order writes |
| Category product listing at 100K+ scale | Account / address reads |
| Typeahead / autocomplete | Exact-match admin queries |
| Sort by relevance | Transactional consistency required |

## DataLoader Required For

Any resolver where the parent type is a collection AND the child requires a DB or provider call:

- `Product.reviews` — GroupedDataLoader keyed by productId
- `Product.pricing` — BatchDataLoader keyed by SKU (calls Pricing provider)
- `Product.inventory` — BatchDataLoader keyed by SKU (calls Inventory provider)
- `Category.productCount` — BatchDataLoader keyed by categoryId
- `Order.lineItems` — GroupedDataLoader keyed by orderId

**Direct DB call from inside a nested resolver = automatic code review rejection.**

## Cache Keys for Big Data

- ✅ `products:category:{categoryId}:page:{cursor}` — cached product page
- ✅ `category:tree:portal:{id}` — full nav tree
- ✅ `seo:{portalId}:{url}` — SEO URL resolution
- ❌ Never cache by `userId` or `accountId` in L2
- ❌ Never cache query results with `totalCount` unless filter is stable

## Sort Contracts

Every cursor-based list must document its canonical sort:

```csharp
// ProductService.GetProductsAsync
// Canonical sort: DisplayOrder ASC, ProductId ASC (tiebreaker)
// Cursor encodes: "displayOrder:{val}:id:{id}"
```

Changing the canonical sort is a breaking change — invalidates all client cursors.
