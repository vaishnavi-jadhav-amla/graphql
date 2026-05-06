---
name: Three Extensibility Systems
description: The GraphQL API has three extensibility mechanisms — Pipeline (per-operation), Interceptors (cross-cutting), Providers (config-only external data) — each solving a different scaling problem
type: project
---

1. **Pipeline** — For complex multi-step operations (e.g., order creation with 7 steps). Steps are ordered, replaceable, and can be disabled via config.
2. **Interceptors** — For cross-cutting concerns across ALL 100+ APIs. Uses wildcard matching (`*` = all, `order*` = prefix). Before/After/Transform hooks.
3. **Providers** — For external data sourcing (inventory, pricing, tax, coupons). ZERO code changes — config-only in appsettings.json.

**Why:** User emphasized that per-API customization doesn't scale to 100+ APIs. One mechanism must cover all. And external data sources (100s of them) should need zero custom code.

**How to apply:** When adding new APIs, they automatically get interceptor coverage. For external data, add a provider config entry — never write custom HTTP client code.
