---
name: Provider Registry Details
description: Config-only external data sourcing via appsettings.json — 7 pre-configured providers (Inventory, Pricing, Tax, Coupons, Shipping, CustomerData, ProductEnrichment) with URL templates and response mapping
type: project
---

The Provider Registry enables external data sourcing with ZERO code changes — purely config-driven via `appsettings.json`.

**Config structure per provider (`ProviderEndpointConfig`):**
- `Name`, `Enabled` (default false — customer flips to true)
- `Url` with `{param}` substitution (e.g., `https://erp.client.com/api/inventory/{sku}`)
- `Method` (GET/POST), `ApiKey`, `ApiKeyHeader`, `BearerToken`, custom `Headers`
- `TimeoutMs` (default 5000), `CacheTtlSeconds`
- `FallbackToZnode` (true = use DB data when provider fails)
- `ResponseMapping` — field → JSON dot-path (e.g., `"Quantity": "data.inventory.quantity"`)

**7 pre-configured providers:**
1. Inventory — `{sku}` lookup, maps quantity/inStock/warehouseId
2. Pricing — `{sku}/{portalId}` lookup, maps retailPrice/salePrice/wholesalePrice/currencyCode
3. Tax — POST with order data, maps taxAmount/taxRate/taxBreakdown
4. Coupons — `{code}/{portalId}` validation, maps isValid/discountType/discountValue
5. Shipping — POST with address/items, maps rates array
6. CustomerData — `{customerId}` lookup, maps loyalty/creditLimit/segment
7. ProductEnrichment — `{sku}` lookup, maps description/specifications/reviews

**Key classes:**
- `ExternalDataProvider` — Universal HTTP client with URL substitution, auth, caching, JSON dot-path parsing
- `ProviderRegistry` — Reads all providers from config, exposes `HasProvider()` and `GetAsync()`
- `IL1CacheForProviders` / `ProviderMemoryCache` — In-memory caching for provider responses

**Why:** Real-world ecommerce has 100s of external data sources. Writing custom HTTP client code per integration doesn't scale. Config-only means customer just fills in URL and field mapping.

**How to apply:** To add a new external source, add a config entry in `appsettings.json` → `GraphQL:Providers`. In service code, call `_providers.GetAsync("ProviderName", params)` and map the result.
