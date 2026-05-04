---
name: Pagination — Cursor-Based (Relay Spec)
description: All list queries must use cursor-based pagination. OFFSET/Skip-Take is forbidden beyond page 10 at 100K+ rows. Relay Connection spec with base64-encoded cursors.
type: project
---

## Why Not OFFSET?

`OFFSET 10000 FETCH NEXT 25 ROWS` = SQL Server reads and discards 10,000 rows. At 100K products:
- Page 1: 25ms
- Page 100: 250ms
- Page 1000: 2500ms
- Page 4000: 10s → timeout

**OFFSET is O(N) where N = offset.** Unusable at scale.

## Cursor Pagination (Required Pattern)

Cursor = last seen sort key, opaquely encoded. Next page query is `WHERE sortKey > lastSeenKey` — O(log N) regardless of page.

## Relay Connection Spec

Every list query returns a `Connection` type:

```graphql
type ProductConnection {
  edges: [ProductEdge!]!
  pageInfo: PageInfo!
  totalCount: Int   # optional — expensive, only if client requests
}

type ProductEdge {
  node: Product!
  cursor: String!
}

type PageInfo {
  hasNextPage: Boolean!
  hasPreviousPage: Boolean!
  startCursor: String
  endCursor: String
}
```

Query arguments:
```graphql
products(
  first: Int           # forward pagination
  after: String        # cursor from previous page
  last: Int            # backward pagination (optional)
  before: String       # cursor for backward page
  filter: ProductFilter
  sort: ProductSort
): ProductConnection!
```

## Cursor Encoding

Cursor is a base64-encoded opaque string. Never expose IDs directly.

```csharp
// Standard cursor = "{sortField}:{value}" base64-encoded
public static string EncodeCursor(int productId)
    => Convert.ToBase64String(Encoding.UTF8.GetBytes($"id:{productId}"));

public static int? DecodeCursor(string? cursor)
{
    if (string.IsNullOrEmpty(cursor)) return null;
    try
    {
        var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(cursor));
        var parts = decoded.Split(':', 2);
        return parts.Length == 2 && int.TryParse(parts[1], out var id) ? id : null;
    }
    catch { return null; }
}
```

For multi-field sorts (e.g., sort by price then id):
```csharp
// Cursor: base64("price:49.99:id:1234")
// WHERE (price > @price) OR (price = @price AND id > @id)
```

## Implementation Template

```csharp
public async Task<ProductConnection> GetProductsAsync(
    int portalId, int? first, string? after,
    int? last, string? before,
    ProductFilter? filter, CancellationToken ct)
{
    first ??= 25;
    if (first > 100) throw new ArgumentException("Max page size is 100.");

    await using var db = await _dbFactory.CreateDbContextAsync(ct);

    var query = db.ZnodePublishProductEntities
        .AsNoTracking()
        .Where(p => p.PortalId == portalId && p.IsActive);

    if (filter?.CategoryId is int cat)
        query = query.Where(p => p.Categories.Any(c => c.CategoryId == cat));

    // Apply cursor
    var afterId = DecodeCursor(after);
    if (afterId.HasValue)
        query = query.Where(p => p.ZnodeProductId > afterId.Value);

    // Fetch N+1 to detect hasNextPage
    var items = await query
        .OrderBy(p => p.ZnodeProductId)
        .Take(first.Value + 1)
        .Select(p => new ProductListItemDto { ... })    // projection
        .ToListAsync(ct);

    var hasNext = items.Count > first;
    if (hasNext) items.RemoveAt(items.Count - 1);

    return new ProductConnection
    {
        Edges = items.Select(p => new ProductEdge
        {
            Node = MapToProductType(p),
            Cursor = EncodeCursor(p.ProductId)
        }).ToList(),
        PageInfo = new PageInfo
        {
            HasNextPage = hasNext,
            HasPreviousPage = afterId.HasValue,    // simplified
            StartCursor = items.Count > 0 ? EncodeCursor(items[0].ProductId) : null,
            EndCursor = items.Count > 0 ? EncodeCursor(items[^1].ProductId) : null
        }
    };
}
```

## HotChocolate Built-in Pagination

HotChocolate provides `[UsePaging]` attribute that auto-generates Relay connections:

```csharp
[UsePaging(MaxPageSize = 100, DefaultPageSize = 25, IncludeTotalCount = false)]
public IQueryable<ProductType> GetProducts([Service] ZnodePublish_Entities db)
    => db.ZnodePublishProductEntities.AsNoTracking();
```

**Use `[UsePaging]` for simple cases.** For complex cursors with multi-field sorts, custom projections, or search integration, implement the connection manually using the template above.

## totalCount is Expensive

`COUNT(*)` at 100K rows on a filtered query can be 100-500ms.

Rules:
- **Do not include `totalCount` in Relay `PageInfo` by default.**
- If client explicitly requests `totalCount`, compute it.
- Cache `totalCount` aggressively if filter is stable (e.g., "products in category X").
- Consider **estimated** count from `sys.partitions` for unfiltered counts — `SELECT SUM(rows) FROM sys.partitions WHERE object_id = OBJECT_ID('TableName')` is instant.

## Sorting Contracts

Any cursor-based query must have a **total order**. If sorting by non-unique field (e.g., price), append ID as tiebreaker:

```csharp
.OrderBy(p => p.Price).ThenBy(p => p.ProductId)
```

Cursor encodes both: `base64("price:49.99:id:1234")`.

## Filters and Cursors Together

Cursor is tied to a specific filter+sort combination. If client changes filter, the cursor is invalid. Two approaches:

1. **Cursor contains filter hash** — reject cursor if filter mismatches (strict, safe).
2. **Ignore cursor on filter change** — treat as page 1 (lenient, UX-friendly).

**Decision: use approach 2** — when filter/sort changes, pagination resets.

## Forbidden Patterns

- ❌ `page`/`pageNumber` arguments on list queries (OFFSET-based)
- ❌ Returning `List<T>` from a list resolver — always return `Connection<T>`
- ❌ `COUNT(*)` on every list query — only when explicitly requested
- ❌ Cursors that expose raw primary keys — always base64 encode

## Existing Code Audit

Any existing service method matching these patterns must be refactored before 100K-row scale:
- `Task<List<T>> GetXxxsAsync(int pageSize, int pageNumber)`
- Code using `.Skip(x).Take(y)` with `x > 0`
- Queries without a total-order `ORDER BY`
