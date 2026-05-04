---
paths:
  - "**/Queries/**/*.cs"
  - "**/Mutations/**/*.cs"
  - "**/Auth/**/*.cs"
  - "**/Schema/ZnodeErrorFilter.cs"
  - "**/Services/**/*.cs"
  - "**/Interceptors/**/*.cs"
---

# Security Rules

## Authorization (REQUIRED on every resolver)

- **Every Query and Mutation method must carry an `[Authorize]` attribute.** No resolver is allowed to be implicitly anonymous.
  - Exception: `login`, `register`, `refreshToken` — explicitly no `[Authorize]`.
- **Use `AuthConstants.*` constants** — never hardcode `"AdminOnly"`, `"Authenticated"`, or role strings.
- **Admin operations on the storefront schema are forbidden.** Cross-schema leakage must not exist.
- **Mutations default to `PolicyAdminOnly` unless the operation is explicitly customer-facing** (cart, wishlist, address, order read). When in doubt, require Admin.

## Input Validation (REQUIRED in mutation methods, not service methods)

- **Reject null or empty required fields at the mutation layer** before calling any service:
  ```csharp
  if (string.IsNullOrWhiteSpace(input.Name))
      throw new ArgumentException("Name is required.", nameof(input.Name));
  if (input.PortalId <= 0)
      throw new ArgumentException("A valid portalId is required.", nameof(input.PortalId));
  ```
- **Never trust client-supplied IDs without ownership checks.** If the operation is scoped to the authenticated user, verify ownership:
  ```csharp
  var accountId = OwnershipGuard.GetAuthenticatedAccountId(context);
  if (input.AccountId != accountId)
      throw new UnauthorizedAccessException("Access denied.");
  ```
- **Validate enum values and ranges.** Unknown enum variants should throw `ArgumentException`, not silently default.

## Error Responses — No Internal Leakage

- **`ZnodeErrorFilter` must never pass stack traces, SQL, or internal type names to the client.** `IncludeExceptionDetails` must be `false` in production.
- **Never throw bare `Exception` or `ApplicationException`** — every thrown type must map to a specific code in `ZnodeErrorFilter`. See `error-handling-logging.md`.
- **Error `extensions.context` must not include passwords, tokens, card numbers, or any PII.** Allowed in context: portalId, productId, cartId, accountId, orderNumber.

## Sensitive Data (NEVER log, include in errors, or return to client)

- Passwords, refresh tokens, API keys
- Credit card numbers, CVV, bank account details
- Social Security Numbers, passport numbers
- Full session cookies
- Mask email in logs: `u***@domain.com` unless auditing specifically requires it

## Multi-Tenant Isolation

- **Every DB query that returns portal-specific data must include a `portalId` filter.** Missing this filter causes cross-tenant data leakage.
- **Every cache key that stores portal data must include `portal:{id}` prefix.** Shared keys with no tenant scope are forbidden for data-bearing values.
- **Throw `CrossTenantAccessException`** (maps to `AUTH_WRONG_TENANT`) whenever a user's JWT `portalId` does not match the requested resource's portal.

## Rate Limiting & DoS Protection

- **Query depth limit: 10.** Configured in `AddMaxExecutionDepthRule(10)`. Do not raise it.
- **Page size limit: 100.** Do not raise without architect approval.
- **Introspection must be disabled in production** (`AllowIntrospection(false)` in prod build).
- **Debug headers (`X-Debug-Level`) require Admin JWT or rotating debug token.** Never trust debug headers from customer tokens.

## Secrets & Configuration

- **Never hardcode SecretKey, connection strings, or provider API keys in C# source.** Read only from `IConfiguration` or environment variables.
- **Never log `IConfiguration` values.** A log grep must not leak secrets.
- **appsettings.json committed to the repo must contain only empty strings or dev-safe placeholders** for all credential fields.
