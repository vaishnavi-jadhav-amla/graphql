---
name: Current Implementation State
description: What is actually built and working vs what is stubbed vs what is pending. Update this file whenever a feature is completed.
type: project
---

## Built & Working (Real EF Core Queries)

| Feature | File | Notes |
|---|---|---|
| Dual-schema setup | `Schema/Storefront/`, `Schema/Admin/` | Storefront + Admin separate schemas |
| JWT + API Key auth | `Auth/` | Both schemes working |
| Pipeline (7 steps) | `Pipeline/Order/Steps/` | All 7 steps, configurable disable list |
| Interceptor system | `Interceptors/` | Before/After/Transform, 3 samples |
| Provider Registry | `Providers/ProviderRegistry.cs` | 7 providers, config-driven |
| `productsBySeoUrl` | `Queries/PIM/ProductQueries.cs` + `Services/PIM/ProductService.cs` | Real EF Core, SEO → publish entities → provider enrichment |
| `websiteEntry` | `Queries/Storefront/WebsiteEntryQueries.cs` + `Services/Storefront/WebsiteEntryService.cs` | Attribute-based, global attribute groups, theme, nav, user context, B2B context, L1 cache |
| L1 + L2 cache | `Caching/L1MemoryCacheLayer.cs`, `Caching/L2RedisCacheLayer.cs` | Both working |

## Stubbed (Placeholder Data — Real Queries Not Yet Written)

| Feature | File | What to do |
|---|---|---|
| `getCategories` / `getCategory` | `Services/PIM/CategoryService.cs` | Query `ZnodePublishCategoryEntity` — materialized path pattern |
| `getBrands` / `getBrand` | `Services/PIM/BrandService.cs` | Query `ZnodePublishBrandEntity` |
| `getCart` | `Services/OMS/CartService.cs` | Query `ZnodeOmsCart` + `ZnodeOmsCartLineItem` |
| `getOrder` / `getOrderByNumber` | `Services/OMS/OrderService.cs` | Query `ZnodeOmsOrder` + line items |
| `getAccount` | `Services/Customer/AccountService.cs` | Query `ZnodeUser` + `ZnodeAccount` |
| `getWishlist` | `Services/Customer/WishlistService.cs` | Query `ZnodeWishList` |
| `getPageBuilderPageBySlug` | `Services/CMS/PageBuilderService.cs` | Query `ZnodePublishPageBuilderPage` + widgets |
| Cart mutations | `Mutations/OMS/CartMutations.cs` | AddToCart, UpdateCartItem, RemoveCartItem, PlaceOrder |
| Auth mutations (register) | `Mutations/Customer/AuthMutations.cs` | login/refresh work; register is stub |

## Not Yet Implemented (Architecture Decided, Code Not Started)

| Feature | ADR | Priority |
|---|---|---|
| **DataLoaders** | ADR-004 | 🔴 HIGH — required before N+1 queries hit production |
| **Diagnostics layer** | ADR-018 to ADR-028 | 🔴 HIGH — `IRequestDebugContext`, correlation IDs, empty-result diagnosers, `diagnose()` query |
| **GZip on IL2Cache** | ADR-003 | 🟡 MEDIUM — before multi-instance Redis deployment |
| **Cursor pagination** | ADR-010 | 🟡 MEDIUM — current `[UsePaging]` uses OFFSET; must migrate for 100K+ rows |
| **Azure Cognitive Search** | ADR-011 | 🟡 MEDIUM — `searchProducts` currently uses SQL LIKE |
| **Field projection** | ADR-012 | 🟡 MEDIUM — services load full JSON columns regardless of selection set |
| **Custom exception types** | ADR-019 | 🟡 MEDIUM — `Diagnostics/Exceptions/*.cs` not created |
| **ProviderHealthTracker** | ADR-025 | 🟠 LOW — extends existing `providers` query with real metrics |
| **Persisted queries** | ADR-007 | 🟠 LOW — production security hardening |
| **SqlBulkCopy for bulk writes** | ADR-017 | 🟠 LOW — needed only for import features |

## Reference: What to Build Next (in priority order)

1. **Diagnostics layer** — Start with `IRequestDebugContext` + `CorrelationIdMiddleware` + `ZnodeErrorFilter` upgrade. This unblocks observability for all subsequent services.
2. **Real CategoryService** — Navigation tree is used by `websiteEntry`. Reference: `znode10-api-migration → ZnodeCategoryService`.
3. **DataLoaders** — After categories work, add `ProductByIdDataLoader`, `CategoryByIdDataLoader`, `PriceBySkuDataLoader`.
4. **Real CartService + PlaceOrder** — The full pipeline needs real cart data to test.
5. **GZip L2 cache** — Before deploying to multi-instance.

## DB Connection

- **Dev DB:** SQL Server at `190.190.0.194`, database `Magicians`
- **Published entities:** `ZnodePublish_Entities` (storefront reads)
- **Transactional entities:** `Znode_Entities` (writes, admin reads)
- **Connection string:** In `appsettings.json` — `ConnectionStrings.ZnodePublishDb`
