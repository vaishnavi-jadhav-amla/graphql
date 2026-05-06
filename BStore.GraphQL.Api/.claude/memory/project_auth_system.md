---
name: Authentication System
description: Dual auth — JWT Bearer (access + refresh tokens) for user sessions and API Key (X-API-Key header) for server-to-server, with role-based policies (Authenticated, AdminOnly)
type: project
---

**Two authentication schemes:**
1. **JWT Bearer** — HS256 tokens with access (30min) + refresh (7 days). Used by storefront users and admin users. Claims: UserId, Role, PortalId, Email.
2. **API Key** — Custom `X-API-Key` header via `ApiKeyAuthHandler`. Used for server-to-server calls. Key configured in `appsettings.json`.

**Authorization policies:**
- `Authenticated` — Requires valid JWT or API Key. Applied to all storefront operations.
- `AdminOnly` — Requires `Role == "Admin"` claim. Applied to all admin operations.

**Key files:**
- `Auth/JwtTokenService.cs` — Generates/validates JWT tokens
- `Auth/ApiKeyAuthHandler.cs` — Custom ASP.NET Core `AuthenticationHandler` for X-API-Key
- `Auth/AuthConstants.cs` — All scheme, policy, role, and claim name constants

**How to apply:** Every resolver must have `[Authorize(Policy = "Authenticated")]` or `[Authorize(Policy = "AdminOnly")]`. No unauthenticated resolvers. Field-level auth uses `[Authorize]` attribute on resolver methods.
