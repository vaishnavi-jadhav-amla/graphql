---
name: Redis Optimization Strategy
description: Redis cost reduction — granular keys, GZip compression, MessagePack, tiered TTLs to cut memory 80%+
type: project
---

## The Problem

Storing full `WebsiteEntryType` as one JSON blob per portal per locale:
- 50-200 KB per key
- Duplicate nested data across portals
- High egress cost on every cache hit
- All-or-nothing invalidation

## Decision: Granular Keys by Component

Split one fat key into per-component keys so shared parts deduplicate across portals/locales.

```
portal:{id}:identity              → ~1 KB   (no locale needed)
portal:{id}:locale:{l}:attributes → ~30 KB  (localized)
portal:{id}:nav:tree              → ~40 KB  (shared if not localized)
portal:{id}:locale:{l}:nav:tree   → ~40 KB  (use only if nav is localized)
portal:{id}:features              → ~2 KB
portal:{id}:media-config          → ~0.5 KB
portal:{id}:theme                 → ~5 KB
```

**Rule: portal identity, features, media-config are locale-independent — never include locale in key.**

## Decision: GZip Before Every Redis SET

JSON compresses 5-10x. Add compression wrapper to `IL2Cache`.

```csharp
// IL2Cache implementation must GZip serialize before SET, decompress after GET
// CompressionLevel.Fastest — ~1-2ms overhead, 80% size reduction
using var ms = new MemoryStream();
using (var gz = new GZipStream(ms, CompressionLevel.Fastest))
    await gz.WriteAsync(jsonBytes);
await redis.StringSetAsync(key, ms.ToArray(), ttl);
```

**Status: IMPLEMENT BEFORE MULTI-INSTANCE DEPLOYMENT**

## Future: MessagePack + LZ4 (When Scaling to 10+ Instances)

When JSON+GZip is no longer enough:
- `MessagePack.NET` with `MessagePackCompression.Lz4Block`
- 3-5x faster deserialization than JSON
- Additional 30-50% size reduction over JSON before compression

## Tiered TTLs by Volatility

| Cache key | TTL | Reason |
|---|---|---|
| `portal:{id}:media-config` | 24 hours | Changes only on deploy |
| `portal:{id}:identity` | 2 hours | Changes only on admin edit |
| `portal:{id}:locale:{l}:attributes` | 1 hour + invalidate on publish | Moderate change frequency |
| `portal:{id}:nav:tree` | 30 minutes | Category tree changes weekly |
| `portal:{id}:theme` | 1 hour | Theme changes infrequently |
| `userContext:*` | NEVER CACHE | Per-user, per-request |
| `b2bContext:*` | NEVER CACHE | Per-user, per-request |

## Don't Cache in Redis

These belong in L1 (in-process) only or not at all:
- `MediaConfig` — loaded from `appsettings.json`, already in memory
- `PortalIdentity` — 1 indexed SQL query, <5ms — L1 only
- `Locales` — tiny table, rarely changes — L1 only
- User/B2B context — **always per-request, never cached**

## Redis Eviction Policy

Set `maxmemory-policy = allkeys-lru` in Redis config. Redis auto-evicts least-used keys when memory limit is reached. Idle portal data evicted automatically.

## Redis Sizing After Optimization

| Optimization | Estimated Memory (100 portals) |
|---|---|
| Current (raw JSON) | ~5 GB |
| + GZip compression | ~1 GB |
| + Granular keys (deduplication) | ~600 MB |
| + MessagePack+LZ4 | ~400 MB |

Target: **Azure Redis P1 (6 GB)** is sufficient post-optimization. C2 Standard ($150/mo) works for non-prod.

## Cache Stampede Protection

Wrap L2 refresh with per-key semaphore to prevent thundering herd:

```csharp
private static readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new();

var semaphore = _locks.GetOrAdd(cacheKey, _ => new SemaphoreSlim(1, 1));
await semaphore.WaitAsync(ct);
try {
    // double-check after acquiring lock
    var cached = await _l2.GetAsync<T>(cacheKey);
    if (cached is not null) return cached;
    var fresh = await loadFromDb();
    await _l2.SetAsync(cacheKey, fresh, ttl);
    return fresh;
} finally { semaphore.Release(); }
```
