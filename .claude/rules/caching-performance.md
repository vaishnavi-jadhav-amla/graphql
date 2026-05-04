---
paths:
  - "**/Caching/**/*.cs"
  - "**/Services/**/*.cs"
  - "**/Queries/**/*.cs"
---

# Caching & Performance Rules

## Redis (IL2Cache) Rules

- **Always GZip before storing to Redis.** Never store raw JSON strings. Use `CompressionLevel.Fastest`.
- **Never use one fat blob per portal.** Split into granular per-component keys. See key design in `project_caching.md`.
- **Locale-independent data must not include locale in the cache key.** Portal identity, features, media-config are locale-independent.
- **userContext and b2bContext are NEVER cached.** They are per-user, per-request. Any service method touching these must NOT call L1 or L2 cache.
- **Wrap L2 refresh in SemaphoreSlim(1,1) per key** to prevent thundering herd on TTL expiry.
- **Media config is never stored in Redis.** It comes from appsettings.json and lives in L1 only.

## Operator invalidation — `flushCaches`

- **Admin-only** mutation (`PolicyAdminOnly`). Prefer **narrow scopes** on shared Redis to avoid SCAN+DEL across the whole `RedisInstanceName` prefix.
- **`L2_PREFIX` requires a non-empty `prefix`** — empty-prefix full Redis wipe is only **`L2_REDIS_PURGE_ALL`** (explicit).
- **`FULL` clears L1 (tracked) + provider cache only** — it does **not** purge L2. Use **`FULL_AND_L2_REDIS_PURGE`** only when the Redis instance prefix is dedicated to this API.
- **Portal-wide L1** after publish: **`PORTAL_L1_WIDE`** + `portalId` (see `Caching/ApplicationCachePortalKeyFilter.cs` for key patterns). **`PORTAL`** is narrower (websiteEntry + nav only).
- **Host/domain changes:** **`L1_PORTAL_HOSTS`** clears all `portal:host:*` entries (all tenants on the process).

## DataLoader Rules

- **Any resolver that loads related data inside a list result MUST use a DataLoader.** No direct DB calls in nested resolvers.
- **DataLoaders go in `DataLoaders/{Domain}/` folder.**
- **DataLoaders use `IDbContextFactory<T>` not `DbContext`** — they execute outside request scope.
- **Inject DataLoaders directly in resolver method parameters** — no `[Service]` attribute, HotChocolate resolves them automatically.

## SQL Performance Rules

- **Connection string must include `MaxPoolSize=200;MinPoolSize=20`** in all deployed environments.
- **Read-only queries always use `.AsNoTracking()`.**
- **`ZnodePublish_Entities` is for reads. `Znode_Entities` is for writes.** Do not use the publish DB for mutations.
- **Never do N+1 queries.** If a resolver loops and calls a service method that hits the DB per iteration, it must be converted to a DataLoader.
- **`Select()` only the columns you need.** Never `.Include()` entire navigation properties if only 1-2 fields are used.

## HotChocolate Query Safety Rules

- **Max query depth is 10** — do not increase without discussing with the architect.
- **Max page size is 100** — never return unbounded lists from a resolver.
- **Expensive fields should have `[Cost]` annotation** if they trigger external provider calls.
- **Disable introspection in production** — set `AllowIntrospection(false)` for the production appsettings profile.

## Caching Tier Decision Guide

| Data | Where to cache | TTL |
|---|---|---|
| appsettings/config values | In-memory (L1 only) | Process lifetime |
| Portal identity (id, name, code) | L1 only | 60s |
| Global attributes JSON | L2 Redis (GZip) | 1 hour |
| Nav tree | L2 Redis (GZip) | 30 min |
| Theme/features | L2 Redis (GZip) | 1 hour |
| Product list by category | L2 Redis (GZip) | 5 min |
| User context / B2B context | Never | — |
| Cart data | Never | — |
| External provider response | L1 (ProviderCache) | Per provider `CacheTtlSeconds` |
