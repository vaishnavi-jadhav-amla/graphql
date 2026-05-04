---
name: Customer Domain — Data & Workflow Reference
description: Tables, columns, business rules, and workflows for Users, Accounts, Addresses, Wishlist. DATA ONLY — coding patterns live in .claude/rules/. Never copy v1/v2 code style.
type: project
---

> **Scope of this file:** What data exists, how it relates, and what the business rules are.
> How to write EF Core queries, services, or resolvers is in `.claude/rules/`.

---

## Users

### Where Data Lives

| Table | Key Columns | Purpose |
|---|---|---|
| `ZnodeUser` | UserId, FirstName, LastName, Email, AccountId, IsActive, PortalId | Core user record |
| `AspNetUsers` | Id (=UserId), Email, PasswordHash, EmailConfirmed | Credentials (ASP.NET Identity) |
| `ZnodeUserProfile` | UserId, ProfileId, IsDefault | User → profile mapping |
| `ZnodeUserAccount` | UserId, AccountId | User linked to B2B account |
| `ZnodeUserProfile` | UserId, ProfileId | Profile override per user (overrides account default) |

### B2C vs B2B Detection

| `ZnodeUser.AccountId` | User type |
|---|---|
| `null` | B2C — individual customer |
| not null | B2B — belongs to a company account |

### JWT Claims Source

| Claim | Where it comes from |
|---|---|
| `userId` | `ZnodeUser.UserId` |
| `portalId` | Portal the user authenticated on |
| `accountId` | `ZnodeUser.AccountId` (null for B2C) |
| `storeCode` | `ZnodePortal.StoreCode` for that portal |

`profileId` is NOT stored in the JWT — it is resolved at query time from the database.

### Login Workflow

```
Client sends email + password
  ↓
Look up AspNetUsers by Email → validate password hash
  ↓
Get ZnodeUser by Email + PortalId (portal scoped — same email can exist on multiple portals)
  ↓
Check ZnodeUser.IsActive = true (suspended users rejected)
  ↓
Get AccountId from ZnodeUser
  ↓
Generate access token (JWT) with userId, portalId, accountId, storeCode claims
Generate refresh token
  ↓
Return tokens to client
```

### Registration Workflow

```
Client sends firstName, lastName, email, password, portalId
  ↓
Validate: email not already used on this portal (ZnodeUser WHERE Email + PortalId)
  ↓
Create AspNetUsers entry (hash password)
  ↓
Create ZnodeUser linked to AspNetUser
  ↓
Assign default registered profile: ZnodePortalProfile.IsDefaultRegistedProfile = true for this portal
  ↓
Create ZnodeUserProfile link to that profile
  ↓
Send welcome email (async, does not block token return)
  ↓
Generate and return tokens (same as login flow)
```

### Profile Resolution for a User (in priority order)

```
1. Explicit profileId in request args → use ZnodeProfile for that ID
2. ZnodeUserProfile.IsDefault = true for this user → their personal default
3. ZnodeAccountProfile.IsDefault = true for their account (B2B only)
4. ZnodePortalProfile.IsDefaultRegistedProfile = true for portal (registered B2C)
5. ZnodePortalProfile.IsDefaultAnonymousProfile = true for portal (guest)
```

---

## Accounts (B2B)

### Where Data Lives

| Table | Key Columns | Purpose |
|---|---|---|
| `ZnodeAccount` | AccountId, CompanyName, ParentAccountId, IsActive, AccountCode | B2B company |
| `ZnodeUserAccount` | UserId, AccountId | Users in account |
| `ZnodeAccountProfile` | AccountId, ProfileId, IsDefault | Account → profile |
| `ZnodeDepartment` | DepartmentId, AccountId, DepartmentName | Departments |
| `ZnodeDepartmentUser` | UserId, DepartmentId | User → department |
| `ZnodeAccountPermission` | AccountId, UserId, PermissionCode | User permissions within account |

### Account Hierarchy

Accounts can be nested via `ParentAccountId` (self-referential):
```
Root Company (ParentAccountId = null)
  └─ Division A (ParentAccountId = root)
        └─ Region East (ParentAccountId = divisionA)
```

Depth is flexible. When loading account data, consider whether parent account pricing/catalogs inherit.

### Permission Codes

| Code | Meaning |
|---|---|
| `WR` | Write/Create — can place orders, add items |
| `ED` | Edit — can edit account data, addresses |
| `DEL` | Delete — can delete records |

These are checked in `ZnodeAccountPermission` for B2B user operations within their account.

### B2B Order Approval

Some B2B accounts require order approval before fulfillment:
- `ZnodeApprovalLevel` defines who must approve and at what order value threshold
- `ZnodeDepartmentUser` determines which department the ordering user belongs to
- Approval chain: user submits → department manager approves → higher levels if configured
- Only after all levels approve does the order proceed to `PROCESSING`

---

## Pricing Priority Chain (Universal — Applies Everywhere)

When resolving a product price for a user, check in this order (first match wins):

```
1. ZnodePriceListUser       (AccountId + SKU) — user-specific override
2. ZnodePriceListAccount    (AccountId + SKU) — account-level
3. ZnodePriceListProfile    via ZnodePortalProfile (PortalId + ProfileId + SKU) — profile tier
4. Default portal price list — base price
```

The external Pricing provider (`ProviderRegistry["Pricing"]`) handles this when enabled. When disabled, resolve from `ZnodePrice` directly.

---

## Addresses

### Where Data Lives

| Table | Key Columns | Purpose |
|---|---|---|
| `ZnodeAddress` | AddressId, Address1, Address2, City, StateName, PostalCode, CountryName, PhoneNumber | Address pool |
| `ZnodeUserAddress` | UserId, AddressId, IsDefaultBilling, IsDefaultShipping, AddressType | User → address link |
| `ZnodePortalCountry` | PortalId, CountryCode | Allowed countries per store |

### Shared Pool Pattern

`ZnodeAddress` is a reusable pool. Multiple users can reference the same physical address. The link is `ZnodeUserAddress`.

This means: editing an address creates a **new** `ZnodeAddress` record and updates the `ZnodeUserAddress` link — it does not overwrite the existing address (because another user might share it).

### Address Types

| `AddressType` | Meaning |
|---|---|
| `Billing` | Billing address only |
| `Shipping` | Shipping address only |
| `Both` | Used for both billing and shipping |

`IsDefaultBilling = true` → pre-selected on billing form  
`IsDefaultShipping = true` → pre-selected on shipping form

### Country Validation

Before saving a shipping address: the `CountryName` / country code must be in `ZnodePortalCountry` for the portal. Addresses in non-whitelisted countries must be rejected.

---

## Wishlist

### Where Data Lives

| Table | Key Columns | Purpose |
|---|---|---|
| `ZnodeWishList` | WishListId, UserId, PortalId, WishListName | Wishlist header |
| `ZnodeWishListItem` | WishListId, PimProductId, Sku, Quantity, AddedDate | Items |

### Rules

- Wishlist is **user-scoped** and **portal-scoped** — both `UserId` and `PortalId` filters required
- One user can have multiple wishlists (distinguished by `WishListName`)
- `AddedDate` is used for "Recently Added" sort order
- When moving to cart: re-validate product availability and pricing (do not use cached wishlist prices)
- If a product is no longer active (`IsActive = false` in `ZnodePublishProductEntity`), show it as "unavailable" — do not silently drop it from the wishlist
