---
name: Query Context — How Business Context Flows Through Every API Call
description: What context is available at each layer, B2B catalog resolution logic, multi-tenant rules. DATA & LOGIC ONLY — coding patterns are in .claude/rules/.
type: project
---

## What Context Exists at Runtime

Every GraphQL request carries two types of context:

### 1. JWT Claims (from auth token — always present if authenticated)

| JWT Claim | Constant | Type | Meaning |
|---|---|---|---|
| `userId` | `AuthConstants.ClaimUserId` | `int` | Logged-in user's ID |
| `portalId` | `AuthConstants.ClaimPortalId` | `int` | Which store the user authenticated on |
| `accountId` | `AuthConstants.ClaimAccountId` | `int?` | B2B account (null for B2C) |
| `storeCode` | `AuthConstants.ClaimStoreCode` | `string` | Short store identifier |

`profileId` is **not** in the JWT. It is resolved at query time from the database.

### 2. Operation Arguments (caller-supplied in the GraphQL query)

| Argument | Where used | Meaning |
|---|---|---|
| `portalId` | Most queries | Which portal to scope data to |
| `localeId` | Products, categories, pages | Which language/locale |
| `profileId` | B2B queries | Which B2B profile scopes the catalog |
| `accountId` | Cart, orders, wishlist, account | Whose data to load |
| `categoryId` | Category/product listing | Which category subtree |
| `seoUrl` | SEO-routed queries | URL to resolve to entity |

---

## Layer Responsibilities

### Resolvers (Queries/Mutations)

Resolvers are the only layer that touches JWT claims. They:
- Extract claims using `AuthConstants.*` constants (never hardcoded strings)
- Validate ownership (`accountId` from JWT must match input `accountId` for customer-scoped ops)
- Resolve missing args from defaults (locale, profile)
- Pass extracted values as typed parameters to the service

Resolvers never pass raw `ClaimsPrincipal` or `HttpContext` to services.

### Services

Services receive `portalId`, `localeId`, `accountId`, `profileId` as method parameters.  
Services do not know about JWT, HTTP headers, or claims. This keeps them testable in isolation.

---

## Context Resolution Order (When Arg Not Supplied)

```
portalId   → from query arg → fallback to JWT portalId claim
localeId   → from query arg → fallback to ZnodePortalLocale.IsDefault = true for that portal
profileId  → from query arg → fallback: see "Profile Resolution" below
accountId  → from query arg → fallback to JWT accountId claim (customer-scoped ops only)
```

---

## B2B Catalog Resolution (Which Products Does This User See?)

The catalog that scopes product/category visibility depends on user type:

### B2C user (AccountId = null)
```
Catalog = ZnodePortalCatalog.PublishCatalogId WHERE PortalId = @portalId
(one query, portal's default catalog)
```

### B2B user with explicit profileId
```
Catalog = ZnodeProfile.PimCatalogId WHERE ProfileId = @profileId
```

### B2B user without explicit profileId
```
Step 1: ZnodeAccountProfile WHERE AccountId = @accountId AND IsDefault = true → ProfileId
Step 2: ZnodeProfile.PimCatalogId WHERE ProfileId = @profileId
Shortcut: use View_GetProfileCatalog view
```

### Guest (not authenticated)
```
Catalog = ZnodePortalCatalog default for portal
Profile = ZnodePortalProfile.IsDefaultAnonymousProfile = true for portal
```

---

## Profile Resolution (Which Profile Applies?)

In priority order (first match wins):

```
1. Explicit profileId in query arg
2. ZnodeUserProfile.IsDefault = true  for this UserId
3. ZnodeAccountProfile.IsDefault = true  for this AccountId (B2B)
4. ZnodePortalProfile.IsDefaultRegistedProfile = true  for this PortalId (registered B2C)
5. ZnodePortalProfile.IsDefaultAnonymousProfile = true  for this PortalId (guest)
```

---

## Pricing Resolution (What Price Does This User Pay?)

In priority order (first match wins, per SKU):

```
1. ZnodePriceListUser     (UserId + SKU)
2. ZnodePriceListAccount  (AccountId + SKU)
3. ZnodePriceListProfile  via ZnodePortalProfile (PortalId + ProfileId + SKU)
4. Default portal price list
```

When `ProviderRegistry["Pricing"]` is enabled: the provider handles this resolution externally and returns the final price. The internal tables are the fallback.

---

## Multi-Tenant Isolation Rules

These prevent cross-tenant data leakage:

1. **Every DB query on `ZnodePublish_Entities` must include `PortalId` in WHERE.**
2. **Every DB query on `Znode_Entities` for customer data must include `PortalId` or `AccountId`.**
3. **B2B product/category filtering uses `ZnodeProfile.PimCatalogId` — not `ZnodePortalCatalog` — when a profile is active.**
4. **BStore child portals inherit parent catalog unless `IsCatalogCustomized = true`.**
5. **If JWT `portalId` ≠ requested resource's portal → `CrossTenantAccessException`.**

---

## Visibility Matrix

| User Type | Auth | Catalog Source | Price Source |
|---|---|---|---|
| Guest (B2C) | None | `ZnodePortalCatalog` default | `IsDefaultAnonymousProfile` price list |
| Registered (B2C) | JWT | `ZnodePortalCatalog` default | `IsDefaultRegistedProfile` price list |
| B2B with profileId | JWT | `ZnodeProfile.PimCatalogId` for profile | Profile price list |
| B2B without profileId | JWT | Default profile's `PimCatalogId` | Account → profile price list |
| BStore tenant | JWT | Own catalog or inherited from parent | Tenant pricing |
| Admin | JWT (Admin role) | All | — |

---

## Context in Prompts

When asking Claude to implement an operation, just describe the user scenario:

```
"Products visible to a B2B account's distributor profile"
→ resolve PimCatalogId from ZnodeProfile for that profileId

"Cart must be own cart only"
→ JWT accountId ownership check in resolver before calling service

"Navigation for a BStore child tenant"
→ check IsCatalogCustomized; use own or parent catalog accordingly

"Pricing for a B2B user with no explicit profileId"
→ resolve default profile via ZnodeAccountProfile.IsDefault, then ZnodePriceListProfile
```
