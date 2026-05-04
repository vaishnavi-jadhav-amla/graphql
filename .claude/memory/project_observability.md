---
name: Observability & Fast Debugging
description: Correlation IDs, structured errors, empty-result explainers, diagnostic query, data-source attribution, per-stage timing, provider health. Turn 2-3 day debug cycles into 30-second investigations.
type: project
---

## The Problem

Typical bug reports:
- "Store not loading"
- "Pricing not updating"
- "Products not coming through"
- "Category list is empty"
- "Cart is 0"
- "Order missing"
- "Payment skipped"
- "Page is blank"
- "Widget data not rendering"

All of these are **"data didn't arrive" symptoms**. The root cause is always one of ~10 patterns:

1. Wrong `portalId` / `localeId` / `accountId` context (multi-tenant isolation)
2. Cache returning stale or cross-tenant data
3. External provider (pricing/inventory/tax) timeout / error / misconfigured
4. Pipeline step disabled via config OR silently failed
5. Interceptor modified result unexpectedly
6. Published data missing for that portal/locale (publish never ran or failed)
7. Authorization filter removed data before resolver saw it
8. SQL query returned empty because of wrong join / wrong catalog / wrong catalog-portal mapping
9. External API returned error but exception was swallowed
10. DataLoader batch failure (one key fails → whole batch empty)

The fix: **every request must output enough structured signal to identify which of these fired within seconds.**

## Core Decision: 7 Observability Pillars

### 1. Correlation ID on Every Request
Every GraphQL request gets a `correlationId` (generated if not sent). It threads through:
- Server log entries (structured logging field)
- Exception extensions
- Outbound provider HTTP calls (`X-Correlation-Id` header)
- Cache operations (logged in debug)
- GraphQL response extensions (always)

Client-visible in response:
```json
{ "data": {...}, "extensions": { "correlationId": "req_3f2a8b1c" } }
```

### 2. Structured Error Envelope (Every Error)
Every error thrown from a resolver is transformed by `ZnodeErrorFilter` into:

```json
{
  "message": "Pricing provider returned empty response",
  "extensions": {
    "code": "PROVIDER_EMPTY",
    "category": "external-provider",
    "correlationId": "req_3f2a8b1c",
    "stage": "service.enrichment.pricing",
    "operation": "getProduct",
    "timestamp": "2026-04-13T14:22:01Z",
    "context": {
      "portalId": 7,
      "productId": 12345,
      "providerName": "Pricing",
      "providerUrl": "https://pricing.example.com/api/v2/price",
      "providerHttpStatus": 200,
      "providerLatencyMs": 1840
    },
    "suggestions": [
      "Check pricing provider is returning data for SKU DRL-500",
      "Verify ResponseMapping in appsettings matches provider payload"
    ]
  }
}
```

**Required fields on every error:** `code`, `category`, `correlationId`, `stage`, `operation`, `context`. `suggestions` when common causes are known.

### 3. Empty-Result Explainer
When a list resolver returns an empty result, it automatically runs a "why empty?" diagnoser and attaches the reason to the response.

**Standard check sequence for `getProducts(portalId: X, categoryId: Y)`:**

```
Products list empty. Running diagnosis...
  [✓] Portal 7 exists and is active
  [✓] User has Authenticated policy
  [✗] CategoryProduct mapping: 0 rows for CategoryId=180 in portal 7
      Hint: Category 180 exists but isn't associated with any catalog used by portal 7
  [i] Last publish for portal 7: 2026-04-08 (5 days ago)
  [i] Portal 7 catalogs: [PimCatalogId=14]
  [i] Products in catalog 14: 847
  [?] Is category 180 part of catalog 14? Run: SELECT * FROM ZnodePimCategoryHierarchy WHERE CategoryId=180 AND PimCatalogId=14
```

This is returned in response `extensions.diagnosis` when debug flag is on, logged always.

### 4. Data-Source Attribution (per field)
Each field in the response can report where its value came from.

Request header: `X-Debug-Trace: data-sources`

Response:
```json
{
  "data": {
    "product": {
      "id": 12345,
      "name": "Drill",
      "price": 79.99,
      "inventory": { "quantity": 42 }
    }
  },
  "extensions": {
    "dataSources": {
      "product.id":               { "source": "sql", "cache": "miss" },
      "product.name":             { "source": "sql.PublishProductJson", "cache": "l2-hit" },
      "product.price":            { "source": "provider:Pricing", "cache": "l1-miss", "latencyMs": 120 },
      "product.inventory.quantity": { "source": "provider:Inventory", "cache": "l1-hit", "ageMs": 8500 }
    }
  }
}
```

Sources:
- `sql` / `sql.{columnName}` — from SQL Server
- `cache:l1` / `cache:l2` — served from cache (with age in ms)
- `provider:{name}` — from external provider
- `default` — hardcoded fallback (⚠️ often signals a problem)
- `computed` — derived from other fields

When you see `"source": "default"` on a field that should have real data — that's the bug.

### 5. Per-Stage Timing
Every request records duration per stage:

```json
"extensions": {
  "timings": {
    "auth.jwt": 2,
    "auth.policy": 1,
    "interceptors.before": 5,
    "service.query.sql": 42,
    "service.enrichment.pricing": 120,
    "service.enrichment.inventory": 85,
    "pipeline.validateCart": 8,
    "pipeline.calculatePricing": 140,
    "interceptors.transform": 3,
    "interceptors.after": 0,
    "total": 412
  }
}
```

When a client says "cart is slow", you look at the timings and immediately see `pipeline.calculatePricing = 140ms` — one provider is slow.

### 6. Pipeline Step Trace
Order pipeline runs produce a structured trace:

```json
"extensions": {
  "pipeline": {
    "name": "OrderPipeline",
    "steps": [
      { "order": 100, "name": "ValidateCartStep",      "status": "ok",      "durationMs": 5 },
      { "order": 200, "name": "CalculatePricingStep",  "status": "ok",      "durationMs": 140 },
      { "order": 300, "name": "ApplyDiscountsStep",    "status": "skipped", "reason": "no-coupon-in-cart" },
      { "order": 400, "name": "CalculateTaxStep",      "status": "failed",  "durationMs": 3010, "error": "Tax provider timeout (3000ms)" },
      { "order": 500, "name": "CreateOrderRecordStep", "status": "not-run", "reason": "previous-step-failed" },
      { "order": 600, "name": "ProcessPaymentStep",    "status": "not-run", "reason": "previous-step-failed" },
      { "order": 700, "name": "SendConfirmationStep",  "status": "not-run", "reason": "previous-step-failed" }
    ]
  }
}
```

"Payment skipped" is now obvious: step 400 failed → step 600 didn't run.

### 7. Provider Health Endpoint
Admin-only query that reports live health of every external provider:

```graphql
query {
  providers {
    name                    # "Pricing"
    enabled                 # true
    url                     # config URL
    callsLast5Min           # 1240
    errorsLast5Min          # 3
    p95LatencyMs            # 180
    lastSuccessAt           # ISO
    lastErrorAt             # ISO
    lastErrorMessage        # "HTTP 503"
    cacheHitRate            # 0.87
    status                  # "healthy" | "degraded" | "down"
  }
}
```

First question during a bug: "Is the provider healthy?" — answered in one query.

## Debug Mode Levels

Request header `X-Debug-Level` (or extensions field) controls verbosity:

| Level | What's included in extensions |
|---|---|
| `off` (default prod) | `correlationId` only |
| `basic` (default dev) | `correlationId`, `timings` |
| `trace` | All of basic + `dataSources`, `pipeline`, cache hits |
| `diagnose` | All of trace + `diagnosis` on empty results + interceptor log |

**Rule:** `diagnose` level requires AdminOnly policy — don't expose internal queries to anonymous users.

## Debug Mode via Admin Token Only

Production API accepts `X-Debug-Level: trace` or higher only if:
- JWT has `role=Admin` OR
- Request carries a valid debug-unlock token (rotated daily, distributed to dev/support staff)

Anonymous or customer-role tokens always get `off` level regardless of header.

## Logging Rules

Every log line is structured with these required fields:
```
correlationId, operation, userId, portalId, stage, durationMs
```

Never use `$""` interpolation in log messages. Always:
```csharp
_logger.LogInformation(
    "Product loaded | CorrelationId:{CorrelationId} | PortalId:{PortalId} | ProductId:{ProductId} | Source:{Source} | DurationMs:{DurationMs}",
    ctx.CorrelationId, ctx.PortalId, productId, source, sw.ElapsedMilliseconds);
```

## Debug-Mode Flag for Runtime Trace

A cross-cutting `IRequestDebugContext` is injected per-request:

```csharp
public interface IRequestDebugContext
{
    string CorrelationId { get; }
    DebugLevel Level { get; }
    void RecordSource(string path, string source, string? cache, long? latencyMs, long? ageMs);
    void RecordStage(string stage, long durationMs);
    void RecordPipelineStep(int order, string name, string status, long durationMs, string? reason);
    void RecordDiagnosis(string field, List<DiagnosisCheck> checks);
}
```

Services call `debug.RecordSource(...)` after every data fetch. The final resolver middleware flattens these into the response extensions.

Cost is near-zero when `Level=off` — all Record* methods become no-ops via a null-object implementation.

## Files to Create

```
Diagnostics/
  ├── CorrelationIdMiddleware.cs            # ASP.NET middleware — generates/extracts ID
  ├── IRequestDebugContext.cs               # Interface above
  ├── RequestDebugContext.cs                # Real implementation (captures data)
  ├── NullRequestDebugContext.cs            # No-op for Level=off
  ├── DebugResponseMiddleware.cs            # HotChocolate middleware — writes extensions
  ├── DataSourceRecorder.cs                 # Helper for services to emit source attribution
  ├── EmptyResultDiagnoser.cs               # Runs "why empty?" checks
  ├── PipelineTracer.cs                     # Captures pipeline step outcomes
  ├── ProviderHealthTracker.cs              # Tracks per-provider metrics
  └── Diagnoses/                            # Per-operation empty-result checks
       ├── ProductListDiagnoser.cs
       ├── CartDiagnoser.cs
       ├── CategoryListDiagnoser.cs
       ├── OrderDiagnoser.cs
       └── PageBuilderPageDiagnoser.cs
```

## Cross-Cutting Integration

All three extensibility systems cooperate with observability:
- **Interceptors** record their own execution in `extensions.interceptors` when `trace` level on
- **Pipeline steps** record via `IPipelineStepTracer` injected into each step
- **Providers** record via `ProviderHealthTracker` (wraps `ExternalDataProvider`)

## Pending Implementation

- [ ] `CorrelationIdMiddleware` + wire to all logs
- [ ] `IRequestDebugContext` + null-object + real impl
- [ ] `DebugResponseMiddleware` writes extensions to response
- [ ] `EmptyResultDiagnoser` per operation (start with products, cart, categories, orders)
- [ ] `ProviderHealthTracker` wrapping `ExternalDataProvider`
- [ ] `PipelineTracer` in `PipelineExecutor`
- [ ] `diagnose(operation, args)` admin query
- [ ] Update `ZnodeErrorFilter` to emit structured envelope
- [ ] Update `GraphQLDiagnosticListener` to record per-stage timings
- [ ] Update all services to call `debug.RecordSource(...)` after data fetch
