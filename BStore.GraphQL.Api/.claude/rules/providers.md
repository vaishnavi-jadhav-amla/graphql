---
paths:
  - "**/Providers/**/*.cs"
---

# External Provider Rules

- Providers are config-driven via `appsettings.json` → `GraphQL:Providers` — ZERO code changes for new sources
- Each provider has: Name, Url (with `{param}` substitution), Method, Auth, Timeout, Cache TTL, ResponseMapping
- `ResponseMapping` uses dot-path JSON navigation (e.g., `"data.inventory.quantity"`)
- Always set `FallbackToZnode: true` so the system uses Znode DB data when the external provider fails
- Provider HTTP calls have configurable timeouts — default 5000ms, never exceed 10000ms
- Provider responses are cached per the `CacheTtlSeconds` setting — respect this for real-time vs. near-real-time data
- When calling `ProviderRegistry.GetAsync()`, always handle null returns gracefully
- NEVER hardcode external URLs in C# code — all external endpoints come from config
