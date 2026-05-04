---
paths:
  - "**/Services/**/*.cs"
---

# Service Layer Rules

## Structure

- Every service has an interface (`IXxxService`) and implementation (`XxxService`)
- Services return GraphQL types from `Types/` folder, **not EF Core entities**
- Mapping from EF entity → GraphQL type happens in private `MapToXxxType()` methods
- All public methods are `async Task<T>` with `CancellationToken ct` as the last parameter — no synchronous variants
- Register as scoped: `services.AddScoped<IXxxService, XxxService>()`

## Required Constructor Parameters

Every service constructor must inject these (in this order):

```csharp
public XxxService(
    ILogger<XxxService> logger,
    ZnodePublish_Entities publishDb,
    IRequestDebugContext debug,      // ← REQUIRED — ADR-022, ADR-027
    IL1Cache l1)                     // ← include if service uses caching
```

- **`IRequestDebugContext debug`** — must be injected in every service. Used for `_debug.RecordSource(...)` and `_debug.CorrelationId` in logs. It is a no-op when debug mode is off — zero performance cost.
- **NEVER inject `HttpContext`** — pass `portalId`, `localeId`, `accountId` etc. as method parameters.

## Data-Source Attribution (Required after every fetch — ADR-022)

After every DB query, cache read, or provider call, call `_debug.RecordSource(...)`:

```csharp
var sw = Stopwatch.StartNew();
var entity = await _publishDb.ZnodePublishProductEntities
    .AsNoTracking().FirstOrDefaultAsync(p => p.ZnodeProductId == id, ct);
_debug.RecordSource("product", "sql", cache: null, latencyMs: sw.ElapsedMilliseconds);
```

## Typed Exceptions for Domain Failures (ADR-019)

Services must throw typed custom exceptions, not return empty/null silently for precondition failures:

| Failure | Throw |
|---|---|
| Portal not found | `KeyNotFoundException` with portalId in message |
| Product/category not published | `NotPublishedException` |
| Catalog not assigned to portal | `CatalogNotAssignedException` |
| Category not in catalog | `CategoryNotInCatalogException` |
| Wrong tenant | `CrossTenantAccessException` |
| Provider timeout | `ProviderTimeoutException` |
| Provider empty response | `ProviderEmptyException` |

Custom exception types live in `Diagnostics/Exceptions/`.

## Structured Logging (Required fields — ADR-027)

Every log call in a service must include:

```csharp
_logger.LogDebug(
    "Operation completed | CorrelationId:{CorrelationId} | Operation:{Operation} | PortalId:{PortalId} | DurationMs:{DurationMs}",
    _debug.CorrelationId, "getProduct", portalId, sw.ElapsedMilliseconds);
```

Required fields: `CorrelationId`, `Operation`, `PortalId`, `Stage`, `DurationMs`. **Never use string interpolation (`$""`) in log messages.**

## Caching Order

Services check in this order: L1 → L2 → DB → external provider. After loading from DB or provider, populate both L1 and L2.

## Empty Results

- **Returning an empty list is valid** — never throw when a list query returns 0 rows.
- If a list service has an `IEmptyResultDiagnoser` registered for it, the diagnoser explains empty results automatically.
- **DO NOT** return a default/empty model to hide a real precondition failure. Return `null` for not-found single entities; throw for precondition failures.
