---
name: Scalability & Capacity Planning
description: 3000 concurrent user capacity plan — sizing decisions, what breaks first, pre-production checklist
type: project
---

## Target: 3000 Concurrent Users

Realistic load model: 3000 active users → ~500 req/sec peak, ~200 req/sec sustained.

## Approved Infrastructure Sizing

| Component | SKU | Notes |
|---|---|---|
| GraphQL API | 4× 4vCPU/8GB instances | Auto-scale at CPU >65% or queue depth >100 |
| Next.js BFF | 4× 2vCPU/4GB instances | BFF cache = biggest traffic reduction lever |
| SQL Server | 8 vCore, 32GB RAM | Business Critical tier |
| SQL Read Replicas | 2 replicas | ZnodePublish_Entities routed to read replica |
| Redis | Premium P1 (6GB, 10K connections) | After compression, fits comfortably |
| Load Balancer/CDN | Azure Front Door / Cloudflare | Edge cache for persisted queries |

## What Breaks First (Ordered by Risk)

1. **SQL connection pool exhaustion** — default 100 is too low. Set `MaxPoolSize=200, MinPoolSize=20` in connection strings.
2. **N+1 queries** — without DataLoaders, nested resolvers fan out. 500 req/sec → 5000+ DB queries/sec.
3. **L1 cache memory pressure** — 4 instances × duplicate caches = waste. L2 Redis becomes authoritative.
4. **JSON deserialization cost** — `GlobalAttributeGroups` JSON column deserialised on every L2 miss. Pre-compute and cache deserialized object.
5. **Cache stampede on TTL expiry** — 120s TTL means thundering herd every 2 min. Use `SemaphoreSlim` per key.

## Pre-Production Non-Negotiables (Before Go-Live)

- [ ] **DataLoaders** — mandatory for any nested resolvers (Product→Reviews, Category→Products)
- [ ] **Connection pool tuning** — `MaxPoolSize=200, MinPoolSize=20` in all connection strings
- [ ] **Cache stampede protection** — `SemaphoreSlim(1,1)` wrapper on L2 cache refresh
- [ ] **Persisted queries** — register queries at BFF startup; lock API to accept only known hashes
- [ ] **SQL read replica routing** — `ZnodePublish_Entities` → read replica; `Znode_Entities` → primary
- [ ] **Health checks** — `/health` endpoint with SQL + Redis probes, checked every 10s by LB
- [ ] **APM** — Application Insights or Datadog, p95/p99 latency dashboards
- [ ] **Query cost analysis** — `[Cost]` annotations on expensive fields; reject queries above budget
- [ ] **Cache invalidation on publish** — Znode publish events must bust L1+L2 cache, not wait for TTL

## Load Test Targets (k6 or NBomber)

- p95 < 300ms, p99 < 800ms, error rate < 0.1%
- Run 30 min sustained @ 3000 users + 5 min spike to 5000

## Connection String Template

```json
"ZnodePublishDb": "Server=...;Database=...;MaxPoolSize=200;MinPoolSize=20;ConnectTimeout=30;"
```

## Future: Persisted Query Lock-Down

When ready for production, enable persisted query store:
```csharp
.UsePersistedQueryPipeline()
.AddReadOnlyFileSystemQueryStorage("./persisted-queries")
```
BFF registers query hashes at startup. Arbitrary client queries rejected in production.
