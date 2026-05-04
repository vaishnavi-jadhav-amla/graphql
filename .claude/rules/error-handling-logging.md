---
paths:
  - "**/Services/**/*.cs"
  - "**/Queries/**/*.cs"
  - "**/Mutations/**/*.cs"
  - "**/Pipeline/**/*.cs"
  - "**/Interceptors/**/*.cs"
  - "**/Providers/**/*.cs"
  - "**/Schema/ZnodeErrorFilter.cs"
---

# Error Handling & Logging Rules

## Exceptions (REQUIRED behaviors)

- **Never throw bare `Exception` or `ApplicationException`.** Use one of:
  - Built-in: `ArgumentException`, `KeyNotFoundException`, `InvalidOperationException`, `UnauthorizedAccessException`, `TimeoutException`
  - Custom (in `Diagnostics/Exceptions/`): `ProviderTimeoutException`, `ProviderEmptyException`, `NotPublishedException`, `CatalogNotAssignedException`, `CategoryNotInCatalogException`, `CrossTenantAccessException`
- **Every exception carries structured context** — include `portalId`, `productId`, `accountId`, etc. as properties on the exception.
- **Never swallow exceptions silently.** A `try { ... } catch { }` without re-throw or log is forbidden. If intentional, add `// INTENTIONAL-SWALLOW: reason` comment.
- **External provider calls** always throw on timeout/error — never return empty silently. `ProviderRegistry.GetAsync` throws `ProviderTimeoutException`, `ProviderEmptyException`, or `ProviderHttpException`.

## Logging (REQUIRED fields in every structured log)

Every log call must include these structured parameters:
- `CorrelationId`
- `Operation` (e.g., "getProduct")
- `UserId` (nullable)
- `PortalId` (nullable)
- `Stage` (e.g., "service.query", "pipeline.step-200")
- `DurationMs` (where applicable)

### Forbidden log patterns

- ❌ `_logger.LogInformation($"Loaded product {id}");` — string interpolation
- ❌ `_logger.LogError(ex.Message);` — loses stack trace and structured fields
- ❌ `Console.WriteLine(...)` — never allowed
- ❌ `_logger.LogInformation("...")` with no structured params on service/pipeline/provider calls

### Required log pattern

```csharp
_logger.LogInformation(
    "Product loaded | CorrelationId:{CorrelationId} | Operation:{Operation} | PortalId:{PortalId} | ProductId:{ProductId} | Source:{Source} | DurationMs:{DurationMs}",
    _debug.CorrelationId, "getProduct", portalId, productId, source, sw.ElapsedMilliseconds);
```

## Log Levels

- **Debug** — Read operations, cache hits/misses, provider calls (routine)
- **Information** — Writes, mutations, significant state changes
- **Warning** — Recoverable issues (provider fallback, stale cache, degraded mode)
- **Error** — Unrecoverable operation failures (provider down, SQL error)
- **Critical** — Infra failures (DB down, Redis unavailable, config invalid)

## Error Envelope Compliance

Every error returned from a resolver must have:
- `extensions.code` (from catalog — see `project_error_catalog.md`)
- `extensions.category`
- `extensions.correlationId`
- `extensions.stage`
- `extensions.operation`
- `extensions.context` (relevant IDs)

Enforcement happens in `ZnodeErrorFilter`. New exception types must be added to the mapping there.

## Data-Source Attribution (REQUIRED in list services)

Service methods must call `_debug.RecordSource(...)` after each data fetch:

```csharp
var product = await _db.ZnodePublishProductEntities
    .AsNoTracking().FirstOrDefaultAsync(p => p.ZnodeProductId == id, ct);
_debug.RecordSource("product", "sql", cache: null, latencyMs: sw.ElapsedMilliseconds);

var price = await _providerRegistry.GetAsync("Pricing", new { sku });
_debug.RecordSource("product.price", "provider:Pricing",
    cache: priceFromCache ? "l1-hit" : "l1-miss",
    latencyMs: priceLatencyMs);
```

Null-object `IRequestDebugContext` makes this a no-op when debug mode is off — cost is negligible.

## Empty-Result Handling

When a list query returns empty:
- Always log at `Information` level with all query parameters
- Let the `IEmptyResultDiagnoser` for the operation run (via `EmptyResultMiddleware`)
- Never throw on empty — empty is valid. Only throw if the root cause is a precondition failure (portal not found, catalog not assigned, etc.)

## Sensitive Data

NEVER log or include in error context:
- Passwords, tokens, refresh tokens, API keys
- Credit card numbers, CVV, bank details
- Social Security Numbers, passport numbers
- Full session cookies

Mask email addresses in logs (`u***@domain.com`) unless specifically needed for auditing.
