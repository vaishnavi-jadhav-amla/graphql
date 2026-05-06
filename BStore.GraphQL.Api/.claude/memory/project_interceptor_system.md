---
name: Interceptor System Details
description: Cross-cutting interceptor system with IBeforeAction, IAfterAction, ITransformResult — uses HotChocolate field middleware with wildcard operation matching for ALL APIs
type: project
---

Interceptors provide cross-cutting extensibility across ALL GraphQL operations without per-API code.

**Three interfaces:**
- `IBeforeAction` — Runs BEFORE resolver. Use for validation, logging, rate limiting. Has `Operations` list and `Order`.
- `IAfterAction` — Runs AFTER resolver. Use for side effects (ERP sync, webhooks). Has `FireAndForget` (default true).
- `ITransformResult` — Modifies resolver result. Use for enrichment (external pricing/inventory overlay).

**Operation matching:** Wildcard support via regex — `"*"` matches all, `"order*"` matches prefix, exact name for specific.

**Samples created:**
- `LogAllOperationsAction` — `["*"]` — logs every operation
- `ErpSyncAfterOrderAction` — `["placeOrder", "addToCart", ...]` — syncs to ERP after order ops
- `ExternalPricingTransform` — `["product", "searchProducts"]` — overlays external pricing on product results

**Implementation:** `InterceptorMiddleware` is a HotChocolate `FieldDelegate` middleware registered via `.UseField<InterceptorMiddleware>()`. It only intercepts root Query/Mutation fields. Auto-discovers all interceptors from DI.

**Why:** User had 100+ APIs needing customization. Per-API pipelines don't scale. ONE mechanism with wildcard matching covers all operations automatically.

**How to apply:** New interceptors just implement the interface, register in DI, and they auto-apply. No changes needed to existing resolvers.
