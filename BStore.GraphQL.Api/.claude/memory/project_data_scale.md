---
name: Data Scale — 100K+ Products, Attributes, Categories
description: Architecture decisions for handling lakhs (100K+) of products, attributes, categories, and orders at 3000 concurrent users. Covers indexing, pagination, search, field projection, partitioning, and read models.
type: project
---

## Scale Targets

| Entity | Volume | Notes |
|---|---|---|
| Products | 1-10 lakh (100K-1M) | Across all portals |
| Product Attributes | 1-5 lakh per portal | EAV explodes at scale |
| Categories | 10K-50K | Deep trees per portal |
| SKUs | 5-50 lakh | Configurable products multiply |
| Orders | 10K-1 lakh/day | Transactional |
| Customer Accounts | 50K-5 lakh | Multi-tenant |
| Concurrent users | 3000 | ~500 req/sec peak |

## Core Principle: Read from Denormalized Published Tables

**NEVER query the EAV (Entity-Attribute-Value) tables directly for reads.**

Znode has two DB layers:
- **Znode_Entities** — normalized OLTP (products, categories, attributes, values in separate tables joined by FK). Transactional writes go here.
- **ZnodePublish_Entities** — denormalized read store. Products with attributes pre-flattened into JSON columns. Categories pre-computed with paths. This is what publish creates.

**Rule:** All storefront read queries in this GraphQL API MUST use `ZnodePublish_Entities`. Admin reads for unpublished data only go through `Znode_Entities`.

**Why:** At 100K products × 50 attributes = 5M EAV rows per join. Cannot be queried at 500 req/sec.

## What Each Published Table Is Optimized For

| Table | Purpose | Key Fields | Indexes Required |
|---|---|---|---|
| `ZnodePublishProductEntity` | Full product read | ProductId, PortalId, LocaleId, PublishProductJson (NVARCHAR MAX) | PortalId+LocaleId; CatalogId; SKU |
| `ZnodePublishCategoryEntity` | Category with path | CategoryId, PortalId, LocaleId, CategoryJson | PortalId+CatalogId+LocaleId; ParentCategoryId |
| `ZnodePublishPortalGlobalAttributeEntity` | Portal attributes | PortalId, LocaleId, GlobalAttributeGroups (JSON) | PortalId+LocaleId |
| `ZnodePublishWebstoreEntity` | Theme/storefront config | PortalId, LocaleId | PortalId+LocaleId |
| `ZnodePublishSEOEntity` | SEO URL routing | PortalId, SEOURL, SEOTypeId | PortalId+SEOURL (unique); SEOTypeId+SEOCode |

**All storefront reads should hit exactly one of these tables per logical data fetch — never a multi-table EAV join.**

## Indexing Mandates (Add if Missing)

Required non-clustered indexes for 100K+ product scale:

```sql
-- Product lookup by portal+locale (most common)
CREATE NONCLUSTERED INDEX IX_PublishProduct_Portal_Locale
  ON ZnodePublishProductEntity (PortalId, LocaleId, IsActive)
  INCLUDE (ZnodeProductId, Sku);

-- SEO URL resolution (every page load)
CREATE NONCLUSTERED INDEX IX_PublishSEO_Portal_Url
  ON ZnodePublishSEOEntity (PortalId, SEOURL)
  INCLUDE (SEOTypeId, SEOCode, SEOId);

-- Category tree by catalog
CREATE NONCLUSTERED INDEX IX_PublishCategory_Portal_Catalog
  ON ZnodePublishCategoryEntity (PortalId, PimCatalogId, LocaleId)
  INCLUDE (CategoryId, ParentCategoryId, DisplayOrder);

-- Products in category
CREATE NONCLUSTERED INDEX IX_PublishProductCategory_Cat
  ON ZnodePublishCategoryProductEntity (CategoryId, PortalId, LocaleId)
  INCLUDE (ZnodeProductId, DisplayOrder);

-- SKU lookup for inventory/pricing providers
CREATE NONCLUSTERED INDEX IX_PublishProduct_Sku
  ON ZnodePublishProductEntity (Sku)
  INCLUDE (ZnodeProductId, PortalId);
```

**Every new query must be reviewed for index coverage before merging.** Use SQL Server Query Store / missing index DMVs in QA to validate.

## Query Rules at Scale

1. **Always `.AsNoTracking()`** for reads — changes are not being tracked anyway, saves memory.
2. **Always project via `.Select(new { ... })`** — never return full entities. EF generates narrower SQL = less IO.
3. **Always paginate** — no unbounded lists. Max 100/page (already enforced).
4. **Use `AsSplitQuery()` cautiously** — helps with cartesian explosion from multiple `.Include()`, but adds round trips. Benchmark before using.
5. **Never `.Include()` a collection on a list query** — classic N+1 generator. Use DataLoader instead.
6. **Use `FOR JSON PATH` in raw SQL** when assembling deeply nested structures — faster than EF materialization at high volume.
7. **Read-only connection string** for `ZnodePublish_Entities` — `ApplicationIntent=ReadOnly` routes to Always-On read replica automatically.

## Product List Query Pattern (Reference Implementation)

```csharp
// PRODUCTService — list by category, paginated, projected
public async Task<ProductListResult> GetProductsByCategoryAsync(
    int categoryId, int portalId, int localeId,
    int first, string? after, CancellationToken ct)
{
    await using var db = await _dbFactory.CreateDbContextAsync(ct);

    // Decode cursor (see project_pagination.md)
    var afterId = DecodeCursor(after);

    var query = db.ZnodePublishCategoryProductEntities
        .AsNoTracking()
        .Where(cp => cp.CategoryId == categoryId
                  && cp.PortalId == portalId
                  && cp.LocaleId == localeId
                  && (afterId == null || cp.ZnodeProductId > afterId));

    var items = await query
        .OrderBy(cp => cp.ZnodeProductId)       // deterministic — required for cursors
        .Take(first + 1)                        // fetch N+1 to detect hasNextPage
        .Join(db.ZnodePublishProductEntities,
              cp => cp.ZnodeProductId,
              p => p.ZnodeProductId,
              (cp, p) => new ProductListItemDto // narrow projection only
              {
                  ProductId = p.ZnodeProductId,
                  Sku = p.Sku,
                  Name = p.Name,
                  SeoUrl = p.SeoUrl,
                  ImageName = p.ImageName,
                  PublishProductJson = p.PublishProductJson  // deserialize later only if needed
              })
        .ToListAsync(ct);

    // Build connection result with hasNextPage + endCursor
    var hasNext = items.Count > first;
    if (hasNext) items.RemoveAt(items.Count - 1);

    return new ProductListResult
    {
        Items = items.Select(MapToProductType).ToList(),
        PageInfo = new PageInfo
        {
            HasNextPage = hasNext,
            EndCursor = items.LastOrDefault() is var last && last != null
                ? EncodeCursor(last.ProductId) : null
        }
    };
}
```

## Attributes at Scale — Lazy Deserialization

`PublishProductJson` is 2-20 KB per product. Do NOT deserialize it for every list query if only `name`, `sku`, `price`, `image` are requested.

**Use field-aware selection:**

```csharp
public async Task<ProductType?> GetProduct(
    int productId,
    IResolverContext ctx,   // HotChocolate gives you the selection tree
    CancellationToken ct)
{
    var selections = ctx.GetSelections(ctx.ObjectType).Select(s => s.Field.Name).ToHashSet();
    var needsAttributes = selections.Contains("attributes") || selections.Contains("specs");

    var product = await _productService.GetByIdAsync(productId, ct);
    if (needsAttributes)
        product.Attributes = await _productService.LoadAttributesAsync(productId, ct);
    return product;
}
```

**HotChocolate supports projection via `[UseProjection]` + EF Core** — evaluate for ZnodePublish columns. If not possible (due to JSON column unpacking), use manual selection-aware loading as above.

## Category Tree at Scale

**Never traverse parent→child recursively** in SQL at query time. ZnodePublishCategoryEntity should have:
- `CategoryPath` column (materialized path: `/1/45/289/`) for ancestry lookups
- `Level` column for depth filtering
- Pre-computed child counts

**Navigation tree cached aggressively:**
- Cache key: `portal:{id}:nav:tree` (or `portal:{id}:profile:{p}:nav:tree` for B2B)
- TTL: 30 minutes
- Busted on Znode publish event
- Limit depth to 3 levels for mega-menu (deeper trees loaded on category page)

## Facets and Filters

**Never compute facets on-the-fly at query time.** At 100K products the DISTINCT aggregate is too expensive.

**Approach:**
- Pre-aggregate per category: `ZnodePublishFacetAggregate` table (CategoryId, AttributeCode, Value, Count)
- Refresh via publish pipeline
- OR delegate to search service (Elasticsearch/Azure Search) which does facets natively

See `project_search_indexing.md` for full search architecture.

## Bulk Operations (Import, Sync)

Any operation writing >100 rows:
- Use `SqlBulkCopy` directly, NOT `SaveChangesAsync` in a loop
- Batch size 5000-10000 rows
- Run outside the request pipeline (background queue)
- Never triggered by a GraphQL mutation for >100 items — reject with `INVALID_OPERATION` and point to admin bulk import

## Partitioning Strategy (Future — >10M rows)

When a published table exceeds 10M rows:
- Partition by `PortalId` range (groups of portals per partition)
- Separate filegroups for hot vs archive partitions
- Apply `ALTER TABLE SWITCH` for old-data archival
- Consider partitioned views if horizontal shard not feasible

## Monitoring Mandates

Enable in production:
- **Query Store** on SQL Server — 30-day retention, top 20 queries by duration/reads
- **Missing Index DMV** review weekly: `sys.dm_db_missing_index_details`
- **Cache hit ratio** on Redis — target >85%
- **GraphQL slow query log** threshold: 500ms (already in `GraphQLDiagnosticListener`)
- **SQL connection pool metrics** — alert at >70% pool utilisation
