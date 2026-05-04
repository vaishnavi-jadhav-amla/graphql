---
paths:
  - "appsettings*.json"
  - "Program.cs"
  - "GraphQLServiceRegistration.cs"
  - "**/Configuration/**/*.cs"
---

# Deployment & Environment Rules

## Secrets — Never in Source

- **`appsettings.json` in the repo contains only empty strings or dev-safe placeholders** for credentials:
  - `SecretKey`: `"REPLACE_IN_ENVIRONMENT"` or empty
  - `ZnodePublishDb` / `ZnodePrimaryDb`: no real server/password
  - Provider `ApiKey` values: empty string
- **Real values come from environment variables or secrets management** (Azure Key Vault, Docker secrets, environment-specific secrets files not committed to git).
- **`appsettings.Development.json` may contain dev DB credentials** if they point to the dev environment only (190.190.0.194 dev SQL). Must be in `.gitignore`.

## Production vs Development Environment Behavior

| Setting | Development | Production |
|---|---|---|
| `IncludeExceptionDetails` | `true` | **`false`** |
| `AllowIntrospection` | `true` | **`false`** |
| `X-Debug-Level` headers | Honored for any Admin JWT | Admin JWT + rotating debug token |
| Logging level | `Debug` | `Information` |
| Detailed error extensions | All fields | `code`, `correlationId` only |

**Enforce via environment check in `GraphQLServiceRegistration.cs`:**
```csharp
var isDev = builder.Environment.IsDevelopment();
options.EnableSchemaIntrospection = isDev;
options.IncludeExceptionDetails = isDev ? IncludeExceptionDetails.AsRequired : IncludeExceptionDetails.Never;
```

## Connection Strings — Required Settings for Scale

All deployed connection strings must include:
```
MaxPoolSize=200;MinPoolSize=20;ConnectTimeout=30;
```

Default `MaxPoolSize=100` is insufficient for 4 instances × peak load. See ADR-005.

Read-replica routing for storefront reads:
```
ApplicationIntent=ReadOnly
```
(Apply to `ZnodePublishDb` only — not the write `ZnodePrimaryDb`)

## Stateless API Requirements

- **No per-request state stored in singleton services.** Singletons must be thread-safe and stateless.
- **`IRequestDebugContext` is `Scoped`** — per-request lifetime. Never inject it into singletons.
- **Cache `IMemoryCache` is singleton** — ensure all keys are fully qualified with tenant/portal scope.
- **Session state is not used.** All user context comes from JWT claims only.

## Deployment Checklist (before every production release)

- [ ] `AllowIntrospection` is `false`
- [ ] `IncludeExceptionDetails` is `Never`
- [ ] All connection strings include `MaxPoolSize=200`
- [ ] Redis connection string is populated (not empty)
- [ ] `GZip` compression on `IL2Cache` is active
- [ ] All required SQL indexes are present on `ZnodePublish_Entities` (see ADR-013)
- [ ] Provider `Enabled: false` for all providers not configured with real endpoints
- [ ] `appsettings.Production.json` does not contain real secrets (must come from env vars)
- [ ] Slow query threshold alert (`500ms`) is configured in monitoring
- [ ] Health endpoint (`/graphql/storefront?health=1`) returns 200 OK

## Adding New Configuration Values

- **All new configuration must live under the `"GraphQL"` section** in `appsettings.json`.
- **Bind to `GraphQLSettings.cs` via `IOptions<GraphQLSettings>`** — never use `IConfiguration["Key"]` raw string access in service code.
- **Every new setting must have a safe default** that is production-appropriate (feature off, caching off) when not explicitly configured.
- **Document new settings in the Configuration Reference section of `CLAUDE.md`.**

## Health and Readiness

- **`health` and `version` queries have no `[Authorize]` attribute** — infrastructure probes must not require tokens.
- **Health check must validate: DB connectivity, Redis connectivity (if enabled), key provider reachability.**
- **Readiness vs liveness:** health query is for both — if DB is unreachable, return unhealthy immediately (don't return 200 OK).
