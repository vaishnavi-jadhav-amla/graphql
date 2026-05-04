# Memory Index

**Copilot / Cursor / any IDE:** the shared “which file do I open?” guide is **`docs/AI-TOOLS-NAVIGATION.md`**. This folder is optional deep index for Claude-style agents; **`docs/`** + root **`CLAUDE.md`** are canonical for everyone.

## User & Feedback
- [User Profile](user_role.md) — Senior architect, values scalable/config-driven patterns, works across all 4 Znode repos
- [Architecture Feedback](feedback_architecture.md) — Reference v1/v2 code, prefer cross-cutting over per-API, config-only external data

## Business Data Model (Read Before ANY Query)
- [Business Data Model](project_business_data_model.md) — Portal/Catalog/Profile/Account chains, published vs transactional tables, visibility matrix, the "wrong data" bug checklist
- [Query Context](project_query_context.md) — How JWT claims + args flow into services, B2B catalog resolution, pricing resolution, multi-tenant rules
- [Dev Environment Reference](project_dev_reference.md) — Dev DB, test portal IDs, JWT generation, common lookup SQL queries

## Domain Knowledge (Extracted from v1/v2 API — Read Before Implementing Each Domain)
- [PIM Domain](domain_pim.md) — Products (tables, published JSON, attribute types, product types, SEO URL resolution, inventory/pricing rules), Categories (materialized path, mega-menu cache), Brands
- [OMS Domain](domain_oms.md) — Cart (guest vs registered, merge on login, quantity validation, stock options), Orders (state machine, sealed states, payment status, calculated totals), Quotes (B2B approval workflow), Shipping, Coupons
- [Customer Domain](domain_customer.md) — Users (B2C vs B2B, JWT claim mapping), Accounts (hierarchy, department approvals, permissions), Addresses (shared pool, portal country whitelist), Wishlist, login/register flow, pricing priority chain
- [Storefront Domain](domain_storefront.md) — Portal config, BStore multi-tenant rules, global attributes (all common attribute codes), theme, payment methods, shipping, tax, feature flags, locale resolution
- [CMS Domain](domain_cms.md) — Page builder (groups, pages, containers, widgets), widget types, SEO metadata, slug → entity resolution, URL redirects, highlights/badges
- [Shared Constants](domain_shared_constants.md) — Product types, publish states, order states, payment status, discount types, attribute types, SEO entity types, filter operators, audit fields, media URL construction, stored procedures still used in this API

## Project — Core Architecture
- [GraphQL API Project](project_graphql_v3.md) — Standalone HotChocolate API replacing v1/v2 REST, queries ZnodePublish_Entities directly
- [Standalone Project Decision](project_standalone_move.md) — Moved from API repo to D:\Base_Code\Znode.Engine.GraphQL\ for clean maintenance
- [Dual Schema Architecture](project_dual_schema.md) — Storefront (/graphql/storefront) and Admin (/graphql/admin) with separate root types
- [Authentication System](project_auth_system.md) — JWT Bearer + API Key dual auth with Authenticated/AdminOnly policies
- [Caching Strategy](project_caching.md) — L1 in-memory (60s) + L2 Redis (600s) + per-provider caching

## Project — Extensibility Systems
- [Three Extensibility Systems](project_extensibility.md) — Pipeline (per-op), Interceptors (cross-cutting), Providers (config-only)
- [Pipeline System Details](project_pipeline_system.md) — 7 ordered steps (100-700) for order creation, configurable disable list
- [Interceptor System Details](project_interceptor_system.md) — IBeforeAction/IAfterAction/ITransformResult with wildcard matching
- [Provider Registry Details](project_provider_system.md) — 7 config-only providers (Inventory, Pricing, Tax, Coupons, Shipping, etc.)

## Project — Implementation
- [JR GraphQL feature speedrun](../../docs/JR-GraphQL-Feature-Speedrun.md) — 90-minute checklist: 5 questions → boilerplate → `dotnet build` → BCP; copy-paste AI prompt
- [Staff GraphQL checklist](../../docs/STAFF-GRAPHQL-CHECKLIST.md) — perf, cache, flushCaches, tenancy, DataLoaders — apply when implementing any new operation
- [Product List by SEO URL API](project_product_seo_api.md) — Reference implementation: SEO URL → products with provider enrichment
- [websiteEntry Query](project_websiteentry.md) — Attribute-based store entry query, two-tier cache (portal cached / user not cached), [GraphQLName] fix, profile-aware nav
- [Current Implementation State](project_current_state.md) — What's built and working vs stubbed vs pending. **Read this first before starting any new API.**
- [Known Build Issues & Fixes](project_build_errors.md) — Ambiguous types, deprecated APIs, locked files, missing packages
- [Pending Implementation Work](project_pending_work.md) — Priority-ordered: Diagnostics layer → CategoryService → DataLoaders → Cart → remaining stubs

## Scale & Performance
- [Scalability Plan](project_scalability.md) — 3000 concurrent users sizing, what breaks first, pre-production checklist
- [Redis Optimization](project_redis_optimization.md) — Granular keys, GZip compression, tiered TTLs, stampede protection. Target: 80% Redis cost reduction vs raw JSON blobs.
- [DataLoader Pattern](project_dataloaders.md) — Mandatory N+1 fix. HotChocolate BatchDataLoader/GroupedDataLoader pattern + pending list.

## Data Scale (100K-1M rows)
- [Data Scale Architecture](project_data_scale.md) — Read from denormalized ZnodePublish_* tables, required indexes, lazy JSON deserialization, bulk ops, partitioning strategy.
- [Cursor Pagination](project_pagination.md) — Relay Connection spec. OFFSET forbidden beyond page 10. Cursor encoding, multi-field sorts, totalCount caveats.
- [Search & Full-Text](project_search_indexing.md) — Azure Cognitive Search (or Elasticsearch). SQL LIKE forbidden. Indexed docs, faceted filters, typeahead, fallback.
- [Field Projection](project_field_projection.md) — Selection-aware loading via IResolverContext. Never deserialize JSON columns unless client requested attributes. 10x throughput improvement.
- [Media CDN](project_media_cdn.md) — All images via CDN, never through API. MediaConfig URL construction. Azure Front Door + Blob Storage.

## Observability & Fast Debugging
- [Observability Architecture](project_observability.md) — 7 pillars: correlation IDs, structured errors, empty-result explainers, diagnostic query, data-source attribution, per-stage timing, provider health. Target: 2-day debug → 30 seconds.
- [Diagnostic Query](project_diagnostic_query.md) — Admin-only `diagnose(operation, args)` runs any operation with full tracing. Includes ProductListDiagnoser, CategoryDiagnoser, CartDiagnoser, OrderDiagnoser, PageBuilderDiagnoser, WebsiteEntryDiagnoser.
- [Error Code Catalog](project_error_catalog.md) — Every error maps to a catalogued code (PROVIDER_TIMEOUT, NOT_PUBLISHED, CATALOG_NOT_ASSIGNED, AUTH_WRONG_TENANT, etc.). 10 categories, standard envelope.
- [Debug Playbook](project_debug_playbook.md) — Symptom → diagnosis query. Self-service for support staff. Covers store empty, cart 0, pricing stale, payment skipped, page blank, widgets not rendering, data stale, cross-tenant leak.

## Operational Rules (Enforced by Claude Code)

Rules live in `.claude/rules/` and apply automatically to matching file paths.

| Rule File | Paths | Summary |
|---|---|---|
| `graphql-resolvers.md` | Queries, Mutations, Schema | ExtendObjectType on correct root (StorefrontQuery/AdminQuery — NEVER legacy Query), [Authorize] required, CancellationToken |
| `types-definitions.md` | Types | [GraphQLName] for ALL acronym properties (B2B→b2bContext), nullable/default patterns, no logic in types |
| `services.md` | Services | IRequestDebugContext required, RecordSource() after every fetch, typed exceptions, structured log fields |
| `dataloaders.md` | DataLoaders, Services, Queries | BatchDataLoader/GroupedDataLoader pattern, IDbContextFactory required, N+1 forbidden |
| `diagnostics.md` | Diagnostics, Logging | IEmptyResultDiagnoser for every list op, RecordSource/RecordTiming/RecordStep protocol, diagnoser check order |
| `database-efcore.md` | Services, DbContext | ZnodePublish_* read-only, AsNoTracking, PortalId+LocaleId+IsActive filters always |
| `caching-performance.md` | Caching, Services | Granular keys, GZip L2, tiered TTLs, never cache userContext, stampede protection |
| `big-data-queries.md` | Services, Queries, DataLoaders | Cursor pagination, SQL index coverage, Azure Search, field projection |
| `error-handling-logging.md` | Services, Queries, Mutations, Pipeline, Providers | Typed exceptions, structured log fields, forbidden patterns |
| `security.md` | Queries, Mutations, Auth, Services | [Authorize] on all resolvers, input validation, portalId filter, no PII in logs |
| `resilience.md` | Services, Pipeline, Providers | Provider fallback, critical vs non-critical steps, tracer protocol, no silent swallows |
| `pipeline-interceptors.md` | Pipeline, Interceptors | Step ordering, tracer protocol, interceptor wildcard matching |
| `providers.md` | Providers | Config-driven only, FallbackToZnode, timeout required, throw on failure |
| `deployment-config.md` | appsettings, Program.cs, GraphQLServiceRegistration | Secrets in env vars, prod introspection=off, MaxPoolSize=200, deployment checklist |

## Agents (invoke by describing the task)

| Agent | When to use |
|---|---|
| `implementation-guide` | "implement", "build", "add", "create" a new API/service/query — produces exact step-by-step instructions |
| `architecture` | Plan a new feature before writing code — design decisions, file lists, type definitions |
| `code-reviewer` | Review code for security, performance, N+1s, auth, caching patterns |
| `diagnostics-advisor` | Bug reported ("cart is 0", "page blank") — maps symptom to diagnostic query; verifies observability in code |
| `data-scale-expert` | Review queries for 100K-1M row scale — pagination, indexes, search vs SQL, DataLoaders |
| `performance-advisor` | Performance and Redis cost review |
| `db-expert` | EF Core queries, SQL optimization, published entity table analysis |
| `build-runner` | Build project, fix compilation errors |
| `api-tester` | Test GraphQL endpoints via build/run/curl |
| `explorer` | Search across all 4 Znode repos for existing implementations |

## Reference & Tools
- [Repository Locations](reference_repos.md) — All 4 Znode repos + GraphQL + Data Library paths
- [Claude Code Configuration](project_claude_config.md) — 10 agents, 14 rules, hooks configured, auto-memory enabled
