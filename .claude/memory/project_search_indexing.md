---
name: Search & Full-Text — External Index (Elasticsearch / Azure Search)
description: Full-text search, faceted filtering, and typeahead at 100K+ products. SQL LIKE is forbidden. External search engine with pre-indexed documents is mandatory.
type: project
---

## The Rule

**SQL `LIKE '%query%'` is forbidden for product/category/content search at this scale.** It triggers full table scans.

All text search must go through a dedicated search service.

## Approved Options

| Option | Use When | Notes |
|---|---|---|
| **Azure Cognitive Search** | Hosted on Azure, low ops overhead | Managed — preferred for cloud deploys |
| **Elasticsearch / OpenSearch** | On-premise or AWS | More control, more ops burden |
| **SQL Server Full-Text Search (FTS)** | < 100K docs AND simple requirements | Last resort — use only if budget forbids #1/#2 |
| **Meilisearch / Typesense** | Typeahead only | Lightweight, fast — consider for autocomplete |

**Decision: Use Azure Cognitive Search by default** (matches the Azure-first infra plan in `project_scalability.md`).

## Indexed Documents

One search index per logical entity. Document = flattened product with all searchable attributes.

### Product index schema

```json
{
  "id": "portal-1-product-12345",
  "productId": 12345,
  "portalId": 1,
  "localeId": 1,
  "sku": "DRL-500",
  "name": "Cordless Drill 18V",
  "nameSuggest": "Cordless Drill 18V",
  "description": "Full description for relevance ranking",
  "brand": "Maxwell",
  "brandId": 45,
  "categoryIds": [180, 1283, 1282],
  "categoryPaths": ["/Tools/PowerTools/Drills"],
  "price": 89.99,
  "salePrice": 79.99,
  "currency": "USD",
  "inStock": true,
  "attributes": {
    "color": ["Red", "Blue"],
    "voltage": "18V",
    "chuckSize": "1/2 inch"
  },
  "tags": ["cordless", "drill", "18v", "power-tool"],
  "createdAt": "2024-01-15T00:00:00Z",
  "displayOrder": 100
}
```

### Required fields

- `id` — unique key (portal+product+locale composite)
- `portalId`, `localeId` — for multi-tenant filtering (every search query filters by these)
- Searchable text fields — `name`, `description`, `tags` (with `nameSuggest` as completion field)
- Facetable fields — `categoryIds`, `brand`, `attributes.*`, `price`, `inStock`
- Sortable fields — `displayOrder`, `price`, `createdAt`

## Indexing Pipeline

**Documents are indexed on Znode publish, never on-demand from storefront.**

```
Znode Publish Event
  → Pull updated products from ZnodePublishProductEntity (delta by PublishStateId)
  → Flatten JSON attributes into Azure Search document shape
  → Batch upload (up to 1000 docs per batch) via Azure.Search.Documents SDK
  → Update index alias (blue/green if full rebuild)
```

Implementation location: background worker or pipeline step (not a GraphQL service).

## Search GraphQL Operation

```graphql
type Query {
  searchProducts(
    query: String!
    portalId: Int!
    localeId: Int
    filters: ProductSearchFilters
    facets: [String!]
    sort: ProductSearchSort
    first: Int
    after: String
  ): ProductSearchResult!
}

type ProductSearchResult {
  edges: [ProductEdge!]!
  pageInfo: PageInfo!
  facets: [FacetResult!]!
  totalCount: Int!
  queryTimeMs: Int!
}

type FacetResult {
  name: String!
  values: [FacetValue!]!
}

type FacetValue {
  value: String!
  count: Int!
}
```

## Search Service Pattern

```csharp
// Services/PIM/ProductSearchService.cs
public class ProductSearchService : IProductSearchService
{
    private readonly SearchClient _searchClient;   // Azure.Search.Documents

    public async Task<ProductSearchResult> SearchAsync(
        string query, int portalId, int? localeId,
        ProductSearchFilters? filters, List<string>? facets,
        int first, string? after, CancellationToken ct)
    {
        var options = new SearchOptions
        {
            Size = first,
            Skip = DecodeOffsetCursor(after),   // Azure Search uses skip; cap at 100K (service limit)
            Filter = BuildFilter(portalId, localeId, filters),
            IncludeTotalCount = true,
            QueryType = SearchQueryType.Full,
            SearchMode = SearchMode.All
        };
        if (facets != null) foreach (var f in facets) options.Facets.Add($"{f},count:20");

        var response = await _searchClient.SearchAsync<ProductSearchDoc>(query, options, ct);

        return MapToResult(response);
    }
}
```

## Typeahead / Autocomplete

Separate operation — uses Azure Search's `SuggestAsync` or a dedicated suggester index.

```graphql
type Query {
  typeahead(query: String!, portalId: Int!, limit: Int = 10): TypeaheadResult!
}
```

Requirements:
- Response < 50ms
- Hit L1 cache for popular queries (keyed by `query + portalId`, TTL 5 min)
- Return multiple categories: products, categories, brands, CMS pages

## Filters at Scale

SQL cannot efficiently filter on JSON attributes. **Faceted filters always go through search service.**

Storefront filter queries:
- ❌ `WHERE PublishProductJson LIKE '%"color":"red"%'` → forbidden
- ✅ Azure Search `$filter=attributes/color/any(c: c eq 'red')` → milliseconds

## Category Page = Search Query

Even "show products in category X" should go through search at 100K+ scale:
```
$filter=portalId eq 1 and localeId eq 1 and categoryIds/any(c: c eq 180)
```

Why: Category product listing + facet counts + sort options is one search call instead of 3 SQL queries.

## When NOT to Use Search

- Single-product lookup by ID → use SQL (covered by clustered index)
- Cart operations → use SQL (writes)
- Inventory / pricing lookups → use ProviderRegistry
- Back-office admin queries with exact filters → use SQL
- Operations requiring transactional consistency → use SQL

## Search Index Freshness

- Acceptable lag: 5 minutes from publish to searchable
- During peak load, batch updates every 60s
- Deletions processed immediately (hard-delete from index on product unpublish)

## Search Service Failure Mode

If Azure Search is unavailable:
- `searchProducts` returns degraded empty result with `queryTimeMs = -1` and a warning
- Frontend falls back to "show all products in category" via SQL (if on a category page)
- Typeahead silently disables
- Never crash the page because search is down

## Pending Implementation

- [ ] `IProductSearchService` interface + Azure implementation
- [ ] Publish-pipeline step to push updates to search index
- [ ] `searchProducts` GraphQL operation (Relay connection)
- [ ] `typeahead` GraphQL operation
- [ ] Category page refactor to use search
- [ ] Fallback mode when search is unavailable
