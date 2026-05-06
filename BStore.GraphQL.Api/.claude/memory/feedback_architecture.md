---
name: Architecture Feedback
description: User feedback on architecture decisions — prefers scalable patterns, config-only solutions, and referencing existing v1/v2 code for business logic
type: feedback
---

- When building new APIs, always check existing v1/v2 code for business logic reference. User explicitly said "Reference available in old files make sure your architecture should follow."
- Don't propose per-API solutions when the user needs cross-cutting coverage. User pushed back when pipeline was suggested for 100 APIs — led to interceptor system.
- External data sourcing must be config-only. User was clear: "less custom API should needed not more simple."
- Keep the project standalone and separate from the main API repo for clean maintenance.

**Why:** User is building a platform used by multiple clients. Patterns must scale to 100+ APIs and 100s of external data sources without requiring custom code per integration.

**How to apply:** Always default to the most scalable approach. If something can be config-driven, make it config-driven. If something can be cross-cutting, don't make it per-API.
