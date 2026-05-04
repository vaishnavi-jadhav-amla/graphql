---
name: GraphQL API Project
description: Znode GraphQL API is a standalone HotChocolate-based project replacing v1/v2 REST APIs, with dual-schema (storefront/admin), pipeline, interceptors, and provider systems
type: project
---

The GraphQL API at `D:\Base_Code\Znode.Engine.GraphQL\` is a clean rewrite — it does NOT wrap the existing REST API. It queries `ZnodePublish_Entities` directly via EF Core.

**Why:** The v1/v2 REST APIs require 239 BFF routes in Next.js. GraphQL reduces this by ~70% with composite queries.

**How to apply:** When building new APIs, always reference the existing v1/v2 code for business logic but follow the GraphQL architecture patterns (dual-schema, service layer, provider enrichment). Never reuse v1/v2 controllers or services directly.
