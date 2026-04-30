# ADR Compliance Map

Every ADR is satisfied by at least one production code path or operational guarantee.
Files referenced are relative to `BStore.GraphQL/src/BStore.GraphQL.Api/`.

| ADR  | Statement | Where it lives |
| ---- | --------- | -------------- |
| 001  | Attribute-based API (GlobalAttributeGroups, no flat hardcoded fields) | `Attributes/AttributeTypes.cs`, `Attributes/AttributeGroupReadService.cs`, `GraphQL/Resolvers/AttributeQueryResolvers.cs` |
| 002  | Granular Redis keys per component (no fat blobs) | `Caching/CacheKeys.cs` (`ProductBase`, `ProductPrice`, `ProductInventory`, `ProductSeo`, `ProductAttributes`) |
| 003  | GZip compression on all L2 values | `Caching/LayeredCacheService.cs` (default `CompressL2Payloads=true`), `Configuration/CachingOptions.cs` |
| 004  | DataLoaders mandatory for nested resolvers | `DataLoaders/ProductByIdDataLoader.cs`, `.AddDataLoader<>()` in `BStoreGraphQLServiceRegistration` |
| 005  | Scale: 4 instances × 200 pool | `Configuration/GraphQLOptions.cs` (`SqlMaxPoolSize=200`), `BStoreGraphQLServiceRegistration.ApplyPoolSize()` |
| 006  | `[GraphQLName]` for acronyms (B2B, PIM, OMS, SEO, URL, SKU, ERP) | `GraphQL/Types/ProductTypes.cs`, `Storefront/WebsiteEntryTypes.cs`, `Attributes/AttributeTypes.cs` |
| 007  | Max depth 10, page size 100, introspection off in prod | `Configuration/GraphQLOptions.cs`, `BStoreGraphQLServiceRegistration` (`AddMaxExecutionDepthRule(10)`, `MaxPageSize=100`, `DisableIntrospection()` outside Development) |
| 008  | `websiteEntry` single entry-point query | `Storefront/WebsiteEntryResolvers.cs` |
| 009  | Read from `ZnodePublish_Entities` only (no EAV joins) | `BStoreGraphQLServiceRegistration` (`AddDbContextFactory<ZnodePublish_Entities>`), `Storefront/`, `Attributes/AttributeGroupReadService.cs` |
| 010  | Cursor-based pagination (Relay) | `GraphQL/Resolvers/ProductConnectionResolvers.cs` (`[UsePaging]`), `ModifyPagingOptions(MaxPageSize=100)` |
| 011  | Full-text search via Azure Cognitive Search | `Search/ISearchProvider.cs`, `Search/AzureCognitiveSearchProvider.cs`, `Search/SqlLikeSearchProvider.cs` (dev fallback) |
| 012  | Selection-aware field projection | `[UseProjection]` on EF resolvers, `GraphQL/Selection/SelectionProjector.cs` for non-EF reads |
| 013  | Required SQL indexes on published tables | `Documentation/SQL_INDEXES.md` |
| 014  | Media via CDN | `Configuration/MediaOptions.cs`, `Media/MediaUrlBuilder.cs` |
| 015  | Materialized-path category tree | `Catalog/ICategoryTreeService.cs`, `Catalog/MaterializedPathCategoryTreeService.cs` |
| 016  | Inventory & pricing TTL ≤ 30s | `Caching/CacheTtlProfile.cs` (`InventoryPricing` clamped to 30s), `GraphQLOptions.InventoryPricingCacheSeconds` |
| 017  | Bulk writes via SqlBulkCopy | `Bulk/IBulkWriter.cs`, `Bulk/SqlBulkCopyWriter.cs` |
| 018  | Correlation id on every request | `Middleware/CorrelationIdMiddleware.cs`, `Diagnostics/RequestDebugContext.cs` |
| 019  | Structured error envelope with code + category | `Common/ErrorCodes.cs`, `Common/ErrorCategory.cs`, `GraphQL/Infrastructure/BStoreGraphQLErrorFilter.cs`, `Common/ErrorMapper.cs` |
| 020  | Empty-result explainer | `Diagnostics/IEmptyResultDiagnoser.cs`, `Diagnostics/ProductListEmptyResultDiagnoser.cs`, `Diagnostics/BStoreListEmptyResultDiagnoser.cs` |
| 021  | `diagnose(operation, args)` admin query | `GraphQL/Resolvers/DiagnoseQueryResolvers.cs` |
| 022  | Data-source attribution | `Diagnostics/IRequestDebugContext.RecordDataSource`, `Diagnostics/DataSource.cs`, surfaced via `extensions.dataSources` (Detailed) |
| 023  | Per-stage timings in `extensions.timings` | `IRequestDebugContext.Stage(name)`, emitted by `BStoreGraphQLDiagnosticListener` |
| 024  | Pipeline step tracing | `IRequestDebugContext.Note`, surfaced via `extensions.pipeline` |
| 025  | Provider health tracking | `Diagnostics/IProviderHealthTracker.cs`, `Diagnostics/ProviderHealthTracker.cs`, `/health/providers` (admin) |
| 026  | Debug levels gated by Admin/token | `Configuration/GraphQLOptions` (`AdminToken`, `AdminTokenHeader`), `CorrelationIdMiddleware` |
| 027  | Structured logging — required fields | `BStoreGraphQLDiagnosticListener` & resolvers log `CorrelationId`, `Operation`, `UserId`, `ClientId`, `Duration`, `Code` |
| 028  | Support-staff debug playbook | `Documentation/SUPPORT_PLAYBOOK.md` |
