---
name: Dual Schema Architecture
description: GraphQL API has two completely separate schemas — Storefront (/graphql/storefront) and Admin (/graphql/admin) with separate root types and auth policies
type: project
---

The API exposes two independent GraphQL schemas via HotChocolate's named schema feature:

| | Storefront | Admin |
|---|---|---|
| Endpoint | `/graphql/storefront` | `/graphql/admin` |
| Root Query | `StorefrontQuery` | `AdminQuery` |
| Root Mutation | `StorefrontMutation` | `AdminMutation` |
| Consumers | Next.js BFF, customer browsers | Admin portal, server-to-server |
| Auth Policy | `Authenticated` | `AdminOnly` |

**Why:** Storefront and admin have fundamentally different security requirements and operation sets. Mixing them risks exposing admin operations to storefront users.

**How to apply:** Every new resolver class must use `[ExtendObjectType(typeof(StorefrontQuery))]` or `[ExtendObjectType(typeof(AdminQuery))]`. Never share a resolver class between schemas. If both schemas need the same data, create two separate resolver methods.
