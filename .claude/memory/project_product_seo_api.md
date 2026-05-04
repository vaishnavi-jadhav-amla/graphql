---
name: Product List by SEO URL API
description: productsBySeoUrl query resolves SEO-friendly URLs to products using ZnodePublishSeoEntity → ZnodePublishProductEntity with external provider enrichment
type: project
---

The `productsBySeoUrl` query is the first real API built following the full GraphQL-layer architecture. It serves as the reference implementation for all future APIs.

**Flow:**
1. Receives SEO URL (e.g., `"electronics"`) + portalId
2. Queries `ZnodePublishSeoEntity` to resolve URL → entity type (Category/Brand/Product) + entity code
3. Checks for redirects (301/302 via `IsRedirect`/`RedirectUrl`)
4. Fetches products from `ZnodePublishProductEntities` filtered by:
   - Category code OR brand code OR SKU (based on SEO type)
   - `IsActive == true`, correct `PublishedVersionId`, `LocaleId`, `PortalId`
5. Supports sorting: name, price, newest (default)
6. Supports pagination: pageSize, pageNumber with hasNextPage/hasPreviousPage
7. Enriches products from external Pricing/Inventory providers via `ProviderRegistry` (parallel `Task.WhenAll`)
8. Maps `ZnodePublishProductEntity` → `ProductType` with pricing, SEO, images

**Return type:** `ProductListResult` with products, totalCount, pagination fields, SEO context (seoTypeName, seoCode, resolvedEntityName), redirect info.

**GraphQL query signature:**
```graphql
productsBySeoUrl(seoUrl: String!, portalId: Int!, localeId: Int, pageSize: Int, pageNumber: Int, sortBy: String): ProductListResult
```

**Why:** This was built as a real working API to prove this GraphQL architecture works end-to-end with DB queries, provider enrichment, and proper SEO URL resolution — referencing how v1/v2 did it in `PublishedPortalDataService.GetSEOEntity()` and `PublishProductService.GetPublishedProductByCategoryCodes()`.

**How to apply:** Use this as the template for all future product/category/brand APIs. Same pattern: resolve entity → query published data → enrich from providers → map to GraphQL types.
