---
name: Debug Playbook — Common Symptoms → Diagnosis Path
description: For each common customer-reported bug ("store not loading", "cart is 0", "pricing stale", etc.), the exact query/command sequence to diagnose in under 60 seconds.
type: project
---

## How to Use This Playbook

For every symptom, run the listed `diagnose(...)` or `providers` query. Attach the response to the bug ticket. You're done.

Every query below is against `/graphql/admin` with an Admin JWT.

## Symptom → Diagnosis

### "Store not loading" / "Website Entry is empty"

```graphql
query {
  diagnose(operation: "websiteEntry", args: { portalId: 7, localeId: 1 }) {
    result durationMs stages { name status durationMs details }
    diagnosis { summary checks { name status message hint query } suggestions }
  }
}
```

Common root causes surfaced:
- `PortalExists: FAIL` → portal ID wrong
- `PortalActive: FAIL` → portal disabled in admin
- `RecentPublish: FAIL` → never published
- `CatalogAssigned: FAIL` → no catalog → empty nav
- `PublishedAttributes: FAIL` → global attributes not published

### "Products not coming through" / "Category page is empty"

```graphql
query {
  diagnose(operation: "getProductsBySeoUrl", args: {
    seoUrl: "Plumbing", portalId: 7
  }) {
    result diagnosis { summary checks { name status message hint query } suggestions }
  }
}
```

Root causes: SEO URL mismatch, category not in catalog, no products published, wrong locale.

### "Pricing is not updating" / "Old price shown"

Step 1 — check provider health:
```graphql
query { providers(name: "Pricing") {
  name enabled callsLast5Min errorsLast5Min p95LatencyMs
  lastSuccessAt lastErrorAt lastErrorMessage cacheHitRate status
} }
```

Step 2 — check field source:
```
POST /graphql/storefront
Headers: X-Debug-Level: trace
{ product(productId: 12345) { id name price } }
```
Response extensions include:
```json
"dataSources": {
  "product.price": { "source": "provider:Pricing", "cache": "l1-hit", "ageMs": 8500 }
}
```

- `"source": "default"` → provider not called (check `Enabled`)
- `"source": "provider:Pricing", ageMs > 30000` → cache TTL too long
- `"source": "sql.fallback"` → provider timed out, fell back

### "Category list is empty"

```graphql
query {
  diagnose(operation: "getCategories", args: { portalId: 7, localeId: 1 }) {
    result diagnosis { summary checks { name status message hint query } }
  }
}
```

Checks: portal catalogs, category publish status, category-catalog mapping, locale availability.

### "Cart is 0" / "Cart items missing"

```graphql
query {
  diagnose(operation: "getCart", args: { accountId: 123, cartNumber: "CN-456" }) {
    result diagnosis { summary checks { name status message hint query } }
  }
}
```

Checks: account exists, cart exists, cart belongs to account (IDOR), items exist in cart, products referenced are published, inventory provider responded.

### "Order missing" / "Order placed but not visible"

```graphql
query {
  diagnose(operation: "getOrderByNumber", args: { orderNumber: "ORD-12345" }) {
    result diagnosis { summary checks { name status message hint query } }
  }
}
```

Checks: order exists in DB, order is in user's scope (IDOR), order status not in hidden state.

### "Payment skipped" / "Order placed without payment"

This is a pipeline issue. Run a post-mortem on the order:
```graphql
query {
  orderDiagnostic(orderNumber: "ORD-12345") {
    pipeline {
      steps { order name status durationMs reason error }
    }
  }
}
```

Look for:
- `status: "skipped"` with `reason: "previous-step-failed"` → cascade failure
- `status: "skipped"` with `reason: "config-disabled"` → check `DisabledSteps` in config
- Missing step 600 entirely → interceptor removed it

### "Page is blank" / "CMS page not rendering"

```graphql
query {
  diagnose(operation: "getPageBuilderPageBySlug", args: {
    slug: "home", portalId: 7
  }) {
    result diagnosis { summary checks { name status message hint query } }
  }
}
```

Checks: page exists by slug, page is published (not draft), page belongs to a group for this portal, widgets have data, widget references valid.

### "Widget data not rendering" / "Some widgets show, others don't"

```graphql
query {
  diagnose(operation: "getPageBuilderPageBySlug", args: {
    slug: "home", portalId: 7
  }) {
    result
    dataSources { path source cache latencyMs }
  }
}
```

Look at `dataSources` for each widget — failed widgets will show `source: "default"` or `source: "error"`.

### "Data looks stale / changes don't appear"

Use the implemented **`flushCaches`** mutation (Admin / Server-to-server token). Prefer **portal-scoped** L1 first; use **`L2_PREFIX`** with a **non-empty** tenant prefix on shared Redis (never rely on empty prefix for L2).

**After publish — portal 7, wide L1 invalidation:**

```graphql
mutation {
  flushCaches(scope: "PORTAL_L1_WIDE", portalId: 7) {
    l1KeysRemoved
    messages
  }
}
```

**L2 only — keys under `RedisInstanceName` + `portal:7:` (adjust prefix to your real L2 key layout):**

```graphql
mutation {
  flushCaches(scope: "L2_PREFIX", prefix: "portal:7:") {
    l2KeysRemoved
    messages
  }
}
```

See `project_caching.md` and root `CLAUDE.md` → *Cache invalidation* for the full scope list.

### "Request is slow"

Run with timing:
```
POST /graphql/storefront
Headers: X-Debug-Level: basic
<your slow query>
```

Response extensions include `timings` — identify the slow stage, then drill down.

### "500 error / unhandled exception"

Every error response has `extensions.correlationId`. Search logs for it:

```
grep "req_3f2a8b1c" /var/log/znode-graphql/*.log
```

Or via log aggregator (App Insights/Datadog):
```
correlationId:"req_3f2a8b1c"
```

Every log entry for that request is tagged — you see the full path.

### "Cross-tenant data leak" (critical security)

If a user reports seeing another tenant's data:
```graphql
query {
  cacheStatus(keyPattern: "*") {
    keys { key tenantsInKey }    # any key with >1 tenant tag is a leak
  }
}
```

Immediately also check logs for `AUTH_WRONG_TENANT` or `CACHE_CROSS_TENANT` codes.

## Self-Service Debugging for Support Staff

Support staff can be given a read-only admin token and the playbook above. Most tickets resolve without developer involvement.

Training doc (1 page):
- Given a bug report, pick the matching symptom.
- Copy-paste the query with the customer's portalId/ids.
- Attach the response JSON to the ticket.
- If `diagnosis.checks` contain any `FAIL`, action the `suggestion`.
- Escalate to dev only if no FAIL checks but result is still wrong.

## Query Templates — Save in Banana Cake Pop

Pre-register the 10 diagnostic queries above as saved operations in BCP (`GraphQLServiceRegistration.cs` BCP examples). Support staff opens BCP, picks the query, fills in the args.

## Pending Implementation

- [ ] `diagnose(operation, args)` query (AdminQuery)
- [ ] `providers(name?)` query (AdminQuery) — already partially present, extend with metrics
- [ ] `orderDiagnostic(orderNumber)` query — reads pipeline history for order
- [ ] `cacheStatus(keyPattern)` query (AdminQuery)
- [ ] `invalidateCache(keyPattern)` mutation (AdminOnly)
- [ ] Saved operations in BCP for each symptom above
- [ ] 1-page support-staff training doc
