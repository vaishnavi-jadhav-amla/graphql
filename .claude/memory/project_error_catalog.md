---
name: Error Code Catalog
description: Standard error codes, categories, and what each means. Every error thrown in the system must map to one of these codes. Makes debugging deterministic — "code PROVIDER_TIMEOUT" has exactly one meaning.
type: project
---

## Structure of Every Error

```json
{
  "message": "Human-readable summary",
  "extensions": {
    "code": "PROVIDER_TIMEOUT",
    "category": "external-provider",
    "correlationId": "req_3f2a8b1c",
    "stage": "service.enrichment.pricing",
    "operation": "getProduct",
    "timestamp": "2026-04-13T14:22:01Z",
    "context": { /* relevant IDs and params */ },
    "suggestions": [ /* optional fix hints */ ]
  }
}
```

## Error Code Catalog

### Category: `validation` — Client supplied bad input

| Code | When | Typical context |
|---|---|---|
| `INVALID_ARGUMENT` | Missing/malformed argument | arg name, value |
| `INVALID_FORMAT` | Wrong format (email, SKU, etc.) | field, expectedFormat |
| `OUT_OF_RANGE` | Value outside allowed range | field, min, max, value |
| `UNSUPPORTED_OPERATION` | Operation not allowed in current state | currentState, attemptedOp |

### Category: `authorization` — Auth failed or insufficient privilege

| Code | When |
|---|---|
| `AUTH_NOT_AUTHENTICATED` | No/invalid token, anonymous access denied |
| `AUTH_TOKEN_EXPIRED` | Token expired |
| `AUTH_INSUFFICIENT_SCOPE` | User lacks required role/policy |
| `AUTH_WRONG_TENANT` | Token's portalId ≠ requested portalId (cross-tenant) |
| `AUTH_IDOR` | User tried to access another user's resource |

### Category: `not-found` — Resource doesn't exist

| Code | When |
|---|---|
| `NOT_FOUND` | Generic — resource not found |
| `PORTAL_NOT_FOUND` | portalId doesn't exist |
| `PRODUCT_NOT_FOUND` | productId/SKU not found in portal |
| `CATEGORY_NOT_FOUND` | categoryId not found |
| `ORDER_NOT_FOUND` | orderId/orderNumber not found |
| `ACCOUNT_NOT_FOUND` | accountId not found |
| `CART_NOT_FOUND` | cartId/cartNumber not found |
| `SEO_URL_NOT_FOUND` | SEO URL doesn't resolve |
| `PAGE_NOT_FOUND` | CMS page slug not found |

### Category: `publish-data` — Published data missing or stale

| Code | When | Suggestion |
|---|---|---|
| `NOT_PUBLISHED` | Entity exists in Znode_Entities but not in ZnodePublish_* | "Run publish in admin" |
| `STALE_PUBLISH` | Last publish > 7 days ago | "Schedule a publish" |
| `CATALOG_NOT_ASSIGNED` | Portal has no catalog mapping | "Assign catalog to portal" |
| `CATEGORY_NOT_IN_CATALOG` | Category not part of portal's catalog | "Map category to catalog" |
| `LOCALE_NOT_PUBLISHED` | No published data for the locale | "Publish for locale X" |

### Category: `external-provider` — External API problem

| Code | When |
|---|---|
| `PROVIDER_DISABLED` | Provider in config but `Enabled=false` |
| `PROVIDER_NOT_CONFIGURED` | Provider key referenced but not in config |
| `PROVIDER_TIMEOUT` | Provider didn't respond within TimeoutMs |
| `PROVIDER_HTTP_ERROR` | Provider returned 4xx/5xx |
| `PROVIDER_EMPTY` | Provider returned 200 but no/empty data |
| `PROVIDER_MAPPING_FAIL` | ResponseMapping didn't resolve expected fields |
| `PROVIDER_CIRCUIT_OPEN` | Too many failures — circuit breaker tripped |

### Category: `pipeline` — Order pipeline failure

| Code | When |
|---|---|
| `PIPELINE_STEP_FAILED` | A step threw an exception |
| `PIPELINE_STEP_SKIPPED_UNEXPECTEDLY` | Step skipped due to config when it shouldn't have been |
| `PIPELINE_VALIDATION_FAILED` | Step 100 (ValidateCart) failed |
| `PIPELINE_PRICING_FAILED` | Step 200 failed |
| `PIPELINE_DISCOUNT_FAILED` | Step 300 failed |
| `PIPELINE_TAX_FAILED` | Step 400 failed |
| `PIPELINE_ORDER_CREATE_FAILED` | Step 500 failed |
| `PIPELINE_PAYMENT_FAILED` | Step 600 failed (payment gateway) |
| `PIPELINE_CONFIRMATION_FAILED` | Step 700 failed (email) — usually non-fatal |

### Category: `cache` — Caching layer problem

| Code | When |
|---|---|
| `CACHE_CORRUPT` | Cached data failed deserialization |
| `CACHE_CROSS_TENANT` | Cache key leaked across tenants (critical — alert) |
| `CACHE_UNAVAILABLE` | L2 Redis down |

### Category: `database` — SQL problem

| Code | When |
|---|---|
| `DB_CONNECTION_POOL_EXHAUSTED` | Pool full, queued timeout |
| `DB_DEADLOCK` | SQL deadlock retry exhausted |
| `DB_TIMEOUT` | Query exceeded command timeout |
| `DB_CONCURRENCY_CONFLICT` | Optimistic concurrency update failed |

### Category: `rate-limit` — Too many requests

| Code | When |
|---|---|
| `RATE_LIMITED_USER` | Per-user quota exceeded |
| `RATE_LIMITED_TENANT` | Per-portal quota exceeded |
| `QUERY_COST_EXCEEDED` | Query cost above budget |
| `QUERY_DEPTH_EXCEEDED` | Query depth > 10 |

### Category: `internal` — Server bug

| Code | When |
|---|---|
| `INTERNAL_ERROR` | Unhandled exception (last resort) |
| `CONFIG_INVALID` | appsettings malformed at startup |
| `DEPENDENCY_FAILED` | Required dependency down |

## Exception → Code Mapping

`ZnodeErrorFilter` maps C# exceptions to codes:

```csharp
public IError OnError(IError error)
{
    var ex = error.Exception;
    var (code, category, suggestions) = ex switch
    {
        ProviderTimeoutException pte    => ("PROVIDER_TIMEOUT", "external-provider",
            new[] { $"Check provider {pte.ProviderName} health",
                    "Consider increasing TimeoutMs in appsettings" }),
        ProviderEmptyException pee      => ("PROVIDER_EMPTY", "external-provider",
            new[] { $"Verify ResponseMapping for {pee.ProviderName}" }),
        NotPublishedException npe       => ("NOT_PUBLISHED", "publish-data",
            new[] { "Run publish in admin" }),
        CatalogNotAssignedException     => ("CATALOG_NOT_ASSIGNED", "publish-data",
            new[] { "Assign a catalog to the portal" }),
        ArgumentException ae            => ("INVALID_ARGUMENT", "validation", Array.Empty<string>()),
        KeyNotFoundException            => ("NOT_FOUND", "not-found", Array.Empty<string>()),
        UnauthorizedAccessException     => ("AUTH_INSUFFICIENT_SCOPE", "authorization", Array.Empty<string>()),
        TimeoutException                => ("DB_TIMEOUT", "database", Array.Empty<string>()),
        _                               => ("INTERNAL_ERROR", "internal", Array.Empty<string>())
    };

    return error.SetCode(code)
                .SetExtension("category", category)
                .SetExtension("correlationId", _debug.CorrelationId)
                .SetExtension("stage", _debug.CurrentStage)
                .SetExtension("suggestions", suggestions);
}
```

## Required Custom Exceptions

Create in `Diagnostics/Exceptions/`:

```csharp
public class ProviderTimeoutException(string providerName, int timeoutMs)
    : Exception($"Provider {providerName} timed out after {timeoutMs}ms") { ... }

public class ProviderEmptyException(string providerName)
    : Exception($"Provider {providerName} returned empty response") { ... }

public class NotPublishedException(string entity, int id)
    : Exception($"{entity} {id} is not published") { ... }

public class CatalogNotAssignedException(int portalId)
    : Exception($"Portal {portalId} has no catalog assigned") { ... }

public class CategoryNotInCatalogException(int categoryId, int catalogId)
    : Exception($"Category {categoryId} is not in catalog {catalogId}") { ... }

public class CrossTenantAccessException(int requestedPortal, int actualPortal)
    : Exception($"Cross-tenant access blocked: requested {requestedPortal}, authorized for {actualPortal}") { ... }
```

## Error Code Usage Rules

1. **Every `throw` in a service maps to a code.** Don't throw generic `Exception`.
2. **Context must include IDs.** Always populate `portalId`, `productId`, `cartId`, etc. in `context`.
3. **Suggestions are for common causes.** Pre-canned hints save 80% of debug time.
4. **Never swallow exceptions silently.** Provider failures must throw → caught by error filter → reported with code.
5. **Sensitive data never in context.** No passwords, tokens, SSNs, CC numbers — even in error payloads.
