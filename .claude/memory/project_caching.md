---
name: Caching Strategy
description: Two-tier caching — L1 in-memory (60s TTL) + L2 Redis distributed (600s TTL) with GZip compression, granular keys, and per-provider caching
type: project
---

## Tiers

**L1 Cache:** Per-process `IMemoryCache` with 60s default TTL. Fast, not shared across instances.
**L2 Cache:** `IDistributedCache` backed by Redis (falls back to `DistributedMemoryCache` if Redis not configured). Shared across all API instances. **Must use GZip compression before storing.**

**Lookup order:** L1 → L2 → Database → External Provider

## Granular Key Design (DECISION: Always split by component, never one fat blob)

```
portal:{id}:identity              → L1 only (1 indexed query, <5ms)
portal:{id}:locale:{l}:attributes → L2 Redis, 1hr TTL + invalidate on publish
portal:{id}:nav:tree              → L2 Redis, 30min TTL
portal:{id}:features              → L2 Redis, 1hr TTL
portal:{id}:theme                 → L2 Redis, 1hr TTL
portal:{id}:media-config          → L1 only (from appsettings, already in-memory)
userContext:*                     → NEVER CACHE (per-request)
b2bContext:*                      → NEVER CACHE (per-request)
```

**Rule: portal identity, features, media-config are locale-independent — never include locale in key.**

## Compression (DECISION: GZip all L2 values)

JSON compresses 5-10×. IL2Cache implementation must GZip before SET, decompress after GET.
`CompressionLevel.Fastest` — ~1-2ms overhead, ~80% size reduction.
Target Redis tier after compression: Azure Redis P1 (6GB) for 100 portals at full load.

## Tiered TTLs

| Key pattern | TTL | Notes |
|---|---|---|
| media-config | In-memory only | Loaded from appsettings |
| portal identity | L1 60s only | Single indexed query |
| theme, features | 1 hour | Low change frequency |
| global attributes | 1 hour + publish invalidation | Moderate change frequency |
| nav:tree | 30 minutes | Category tree |
| userContext, b2bContext | **Never cache** | Always per-request |

## Stampede Protection (DECISION: SemaphoreSlim per key)

Wrap L2 refresh with `SemaphoreSlim(1,1)` per cache key to prevent thundering herd on TTL expiry.
See `project_redis_optimization.md` for full implementation pattern.

## Provider caching

Each external provider has its own `CacheTtlSeconds` setting.
Cached via `IL1CacheForProviders` / `ProviderMemoryCache`.

## Config in appsettings.json

```json
"Cache": {
  "EnableL1": true,
  "L1DefaultTtlSeconds": 60,
  "EnableL2": false,
  "RedisConnectionString": "",
  "RedisInstanceName": "znode-gql:"
}
```

## Key files

- `Caching/L1MemoryCacheLayer.cs` — `IL1Cache` (tracked keys, `RemoveByPrefix`, `RemoveWhere`, `RemoveAllRegisteredKeys`)
- `Caching/L2RedisCacheLayer.cs` — `IL2Cache` (GZip; `RemoveByPrefixAsync` uses Redis SCAN+DEL)
- `Caching/RedisKeyScanner.cs` / `IRedisKeyScanner.cs` / `NoOpRedisKeyScanner.cs` — L2 prefix delete implementation
- `Caching/CacheFlushCoordinator.cs` — `ICacheFlushCoordinator` + `CacheFlushScope` orchestration
- `Caching/ApplicationCachePortalKeyFilter.cs` — Portal-scoped L1 keys that are not simple prefixes
- `Mutations/Storefront/CacheFlushMutations.cs` — GraphQL `flushCaches` on storefront schema (admin endpoint may be off)
- `Mutations/Admin/AdminCacheMutations.cs` — Same mutation on admin schema when registered
- `Types/Storefront/CacheFlushResultType.cs` — Mutation result (counts + `messages`)
- `Providers/IL1CacheForProviders.cs` — Provider-specific cache (tracked keys; prefix flush)

## Operator invalidation — `flushCaches`

GraphQL mutation: **`flushCaches(scope: String!, prefix: String, portalId: Int): CacheFlushResultType`** (HotChocolate camelCase). **Policy:** `AdminOnly` (Admin or ServerToServer JWT / API key per project auth).

Returns **`l1KeysRemoved`**, **`l2KeysRemoved`**, **`providerKeysRemoved`**, **`messages`** (operational notes, skipped layers, what was *not* cleared).

### Scope tokens (summary)

| Scope | portalId | prefix | Notes |
|------|----------|--------|--------|
| `L1_ALL` | — | — | All tracked `IL1Cache` keys only |
| `L1_PREFIX` | — | required | Prefix match on tracked L1 keys |
| `L1_PORTAL_HOSTS` / `L1_HOST_RESOLUTION` | — | — | Clears `portal:host:` map (every tenant) |
| `L1_PORTAL_LEGACY` | required | — | `store:header:{id}` + `portal:config:{id}` |
| `PORTAL` / `PORTAL_TENANT` | required | — | `websiteEntry:{id}:` + `nav:tree:{id}` |
| `PORTAL_L1_WIDE` / `L1_PORTAL_WIDE` | required | — | Wide portal L1 (+ list/SEO keys for portal; not `product:{id}`) |
| `L2_PREFIX` | — | **required non-empty** | Under `RedisInstanceName` + prefix |
| `L2_REDIS_PURGE_ALL` / `L2_INSTANCE_PURGE` | — | — | **All** keys under `RedisInstanceName` — use only if prefix is API-dedicated |
| `PROVIDERS_ALL` | — | — | All tracked provider entries |
| `PROVIDERS_PREFIX` | — | required | e.g. `provider:` |
| `FULL` | — | — | L1 all + providers; **no** L2 SCAN |
| `FULL_AND_L2_REDIS_PURGE` / `FULL_DESTROY_L2` / `FULL_LEGACY` | — | — | L1 + providers + full L2 instance namespace |

### Not cleared by `IL1Cache` scopes

- HTTP **rate limit** keys (`RateLimitBeforeInterceptor` on raw `IMemoryCache`)
- **`znode-perms:{roleName}`** (`ZnodePermissionService` — use `InvalidateRole` when roles change)

### Shared Redis / cost

Prefer **`L2_PREFIX`** with a tenant-specific logical prefix (align with ADR-002, e.g. `portal:{id}:`) instead of **`L2_REDIS_PURGE_ALL`**. Full-instance purge runs **SCAN on each master** and **DEL** — high latency and billing impact on large keyspaces.

## Redis maxmemory policy

Set `maxmemory-policy = allkeys-lru` on Redis server. Idle portal data auto-evicted when memory pressure is high.
