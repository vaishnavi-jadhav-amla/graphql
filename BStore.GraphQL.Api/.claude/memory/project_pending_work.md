---
name: Pending Implementation Work
description: What is next to build, in priority order. See project_current_state.md for what is already done.
type: project
---

## Priority 1 — Diagnostics Layer (ADR-018 to ADR-028)

Unblocks debugging for all other services. Every future service depends on this.

- [ ] `Diagnostics/IRequestDebugContext.cs` + `RequestDebugContext.cs` + `NullRequestDebugContext.cs`
- [ ] `Diagnostics/CorrelationIdMiddleware.cs` — assign/read `X-Correlation-Id`
- [ ] `Diagnostics/DebugResponseMiddleware.cs` — write `extensions.*` to response
- [ ] `Diagnostics/PipelineTracer.cs` — `IPipelineStepTracer`
- [ ] `Diagnostics/ProviderHealthTracker.cs` — call metrics singleton
- [ ] `Diagnostics/Exceptions/` — custom exception types: `ProviderTimeoutException`, `ProviderEmptyException`, `NotPublishedException`, `CatalogNotAssignedException`, `CategoryNotInCatalogException`, `CrossTenantAccessException`
- [ ] Upgrade `ZnodeErrorFilter.cs` — map all custom exceptions to error catalog codes
- [ ] `Diagnostics/Diagnoses/WebsiteEntryDiagnoser.cs` — first diagnoser (reference impl)
- [ ] Register all of the above in `GraphQLServiceRegistration.cs`
- [ ] Add `diagnose(operation, args)` admin query (`Queries/Admin/DiagnosticQueries.cs`)

**Reference:** `.claude/rules/diagnostics.md`, `.claude/memory/project_observability.md`, `.claude/memory/project_diagnostic_query.md`

## Priority 2 — Real CategoryService

Used by `websiteEntry` navigation and category page queries.

- [ ] `Services/PIM/CategoryService.cs` — real EF Core against `ZnodePublishCategoryEntity`
  - `GetNavigationTreeAsync(portalId, profileId?)` — materialized path pattern, Level ≤ 3 for mega-menu
  - Cache key: `portal:{id}:nav:tree` (TTL 30min)
- [ ] `Diagnostics/Diagnoses/CategoryDiagnoser.cs`

**Reference:** `project_data_scale.md` (materialized path), v1 code: `znode10-api-migration → ZnodeCategoryService.cs`

## Priority 3 — DataLoaders (ADR-004)

Required before any list query that has nested resolvers goes to production.

- [ ] `IDbContextFactory<ZnodePublish_Entities>` registration
- [ ] `DataLoaders/PIM/ProductByIdDataLoader.cs`
- [ ] `DataLoaders/PIM/PriceBySkuDataLoader.cs`
- [ ] `DataLoaders/PIM/InventoryBySkuDataLoader.cs`
- [ ] `DataLoaders/PIM/CategoryByIdDataLoader.cs`
- [ ] `DataLoaders/OMS/OrderByIdDataLoader.cs`

**Reference:** `.claude/rules/dataloaders.md`, `project_dataloaders.md`

## Priority 4 — Real CartService

Needed to test the full PlaceOrder pipeline.

- [ ] `Services/OMS/CartService.cs` — real EF Core against `ZnodeOmsCart` + `ZnodeOmsCartLineItem`
- [ ] `Diagnostics/Diagnoses/CartDiagnoser.cs`
- [ ] Cart mutations: `AddToCart`, `UpdateCartItem`, `RemoveCartItem`, `ApplyCoupon`

## Priority 5 — Remaining Stubbed Services

In order of customer impact:

- [ ] `Services/PIM/BrandService.cs`
- [ ] `Services/Customer/AccountService.cs` (register + login work; address/profile pending)
- [ ] `Services/OMS/OrderService.cs`
- [ ] `Services/Customer/WishlistService.cs`
- [ ] `Services/CMS/PageBuilderService.cs`

Each service needs: real EF Core queries + `IEmptyResultDiagnoser` + L1 caching.

## Priority 6 — GZip on IL2Cache (ADR-003)

Before deploying to multi-instance.

- [ ] Wrap `L2RedisCacheLayer` with GZip compress/decompress
- [ ] Target `CompressionLevel.Fastest`

## Priority 7 — Cursor Pagination (ADR-010)

Before any list query reaches > 1000 rows in production.

- [ ] Replace `[UsePaging]` (OFFSET-based) with manual cursor pagination on `ProductQueries`, `CategoryQueries`
- [ ] Follow `project_pagination.md` implementation template

## How to Build Each Service

Follow the `productsBySeoUrl` implementation as the reference:
1. Reference v1/v2 service: `znode10-api-migration → Libraries → Znode.Engine.Services → {Domain}Service.cs`
2. Query `ZnodePublish_*` tables (not EF entities directly)
3. Enrich with providers after DB load
4. Inject `IRequestDebugContext`, call `RecordSource()` after each fetch
5. Register in `GraphQLServiceRegistration.cs`
6. Add `IEmptyResultDiagnoser` for list operations

See `project_current_state.md` for full status.
