---
name: Business Data Model — Portal/Account/Profile/Catalog Chains
description: The complete business entity relationship map for Znode. Read this before writing any query that involves portal, account, profile, catalog, locale, pricing, or B2B scoping. This is what determines "what data is visible to whom."
type: project
---

## The Core Rule: Everything Scopes Through Portal + Catalog

Every storefront query must filter by at minimum **two axes**:
1. `PortalId` — which store/tenant
2. `PimCatalogId` — which product catalog is assigned to this portal (or profile)

Missing either filter = cross-tenant data leakage or wrong product set.

---

## Portal Types (ZnodePortal.IsBStore + ParentPortalId)

| Type | IsBStore | ParentPortalId | Who uses it |
|---|---|---|---|
| **B2C Portal** | `false` | `NULL` | Direct-to-consumer store — anonymous + registered customers |
| **B2B Portal** | `false` | `NULL` | Business accounts with profiles + contract pricing |
| **BStore Root** | `true` | `NULL` | Multi-tenant SaaS parent — owns child tenant stores |
| **BStore Child (Tenant)** | `true` | `[parentPortalId]` | Individual tenant store under the BStore |

**Important B2B flags on ZnodePortal:**
- `IsBStore` — is this a BStore (multi-tenant)?
- `IsBStoreLoginRequired` — must be logged in to browse
- `IsBStoreShowPricing` — show prices to guest?
- `IsBStoreUsersSelfRegister` — allow self-registration?
- `IsCatalogCustomized` — tenant has its own catalog (not inherited from parent)

---

## Chain 1: Portal → Catalog (What products does a store show?)

```
ZnodePortal
  └── ZnodePortalCatalog (PortalId → PublishCatalogId)
        └── ZnodePimCatalog (PimCatalogId = PublishCatalogId)
              └── ZnodePublishProductEntity (filter by PimCatalogId)
```

**Key columns:**
- `ZnodePortalCatalog.PortalId` — which portal
- `ZnodePortalCatalog.PublishCatalogId` → maps to `ZnodePimCatalog.PimCatalogId`
- One portal typically has one primary catalog; can have multiple

**Published tables:** Query `ZnodePublishProductEntity` with `PortalId + PimCatalogId` filters.

---

## Chain 2: Account → Profile → Catalog (B2B: what can this account see?)

```
ZnodeAccount (AccountId)
  └── ZnodeAccountProfile (AccountId → ProfileId, IsDefault)
        └── ZnodeProfile (ProfileId → PimCatalogId)
              └── ZnodePimCatalog
                    └── ZnodePublishProductEntity (filter by PimCatalogId)
```

**Key columns:**
- `ZnodeAccountProfile.IsDefault` — which profile applies when not explicitly specified
- `ZnodeProfile.PimCatalogId` — the catalog this profile can see (B2B catalog scoping)
- One account can have multiple profiles, each with a different catalog

**Shortcut view:** `View_GetProfileCatalog` — returns catalogs for a profile
**Shortcut view:** `View_AccountProfileList` — returns all profiles for an account

**B2B Rule:** When `profileId` is known, filter products by `ZnodeProfile.PimCatalogId`. This is why `websiteEntry` accepts an optional `profileId` param — to scope the navigation tree to what the B2B account can see.

---

## Chain 3: Locale (What language/translation?)

```
ZnodePortal
  └── ZnodePortalLocale (PortalId → LocaleId, IsDefault)
        └── ZnodeLocale (LocaleId, Code e.g. "en-US", "fr-FR")
```

**Key columns:**
- `ZnodePortalLocale.IsDefault` — default locale when none specified
- `ZnodeLocale.Code` — IETF language tag ("en-US", "de-DE", "fr-CA")

**Published tables:** `ZnodePublishProductEntity` and `ZnodePublishCategoryEntity` both have `LocaleId` — always filter on it.

---

## Chain 4: Pricing (What price does this account/profile pay?)

Two pricing sources, applied in priority order:

### Priority 1 — Account-level price list (overrides everything)
```
ZnodeAccount
  └── ZnodePriceListAccount (AccountId → PriceListId)
        └── ZnodePrice (PriceListId, SKU → RetailPrice, SalePrice)
```

### Priority 2 — Portal + Profile price list (default for profile type)
```
ZnodePortalProfile (PortalId + ProfileId → PortalProfileId)
  └── ZnodePriceListProfile (PortalProfileId → PriceListId)
        └── ZnodePrice (PriceListId, SKU → RetailPrice, SalePrice)
```

**Shortcut view:** `View_GetAssociatedProfileToPriceList` — price lists for a portal-profile pair
**Shortcut view:** `View_GetAssociatedPortalToPriceList` — all price lists for a portal

**Rule:** External Pricing provider (`ProviderRegistry["Pricing"]`) handles this lookup when enabled. When disabled, fall back to `ZnodePrice` table directly.

---

## Chain 5: User → Account → Profile (Who is this person?)

```
ZnodeUser (UserId, Email, AccountId?)
  └── ZnodeAccount (AccountId, CompanyName)
        └── ZnodeAccountProfile (→ ZnodeProfile)
              └── ZnodeUserProfile (UserId → ProfileId) [optional user-level override]
```

**Key columns:**
- `ZnodeUser.AccountId` — nullable; NULL = B2C guest/registered user, not NULL = B2B user
- `ZnodeUserProfile.ProfileId` — user's specific profile override (overrides account-level default profile)

**Rule:** JWT claims carry `userId`, `accountId`, `portalId`, `storeCode`. `profileId` is resolved at query time from `ZnodeAccountProfile.IsDefault` unless explicitly passed.

---

## Complete Visibility Matrix

| User Type | Auth | Portal | Catalog Source | Pricing Source |
|---|---|---|---|---|
| Guest (B2C) | None | Any B2C portal | `ZnodePortalCatalog → PimCatalogId` | Default portal price list |
| Registered (B2C) | JWT | Any B2C portal | `ZnodePortalCatalog → PimCatalogId` | `IsDefaultRegisteredProfile` price list |
| B2B Account User | JWT | B2B portal | `ZnodeProfile.PimCatalogId` (account's default profile) | Account price list → Profile price list |
| B2B with specific Profile | JWT + profileId | B2B portal | `ZnodeProfile.PimCatalogId` (selected profile) | Profile-specific price list |
| BStore Tenant | JWT | BStore child portal | Child portal catalog (or inherited parent) | Child portal pricing |
| Admin | JWT (role=Admin) | Any | All catalogs | — |

---

## Published Tables Reference (ZnodePublish_Entities — Used for all storefront reads)

| Table | Key Columns | When to use |
|---|---|---|
| `ZnodePublishProductEntity` | PortalId, PimCatalogId, LocaleId, IsActive, Sku, ZnodeProductId | Product list/detail queries |
| `ZnodePublishCategoryEntity` | PortalId, PimCatalogId, LocaleId, CategoryPath, Level | Category tree, mega-menu |
| `ZnodePublishCategoryProductEntity` | CategoryId, ZnodeProductId, PortalId, DisplayOrder | Products in a category |
| `ZnodePublishSEOEntity` | PortalId, SEOURL, SEOTypeId, SEOCode | SEO URL → product/category/page routing |
| `ZnodePublishPortalGlobalAttributeEntity` | PortalId, LocaleId, GlobalAttributeGroupsJson | Portal custom attributes (theme, config) |
| `ZnodePublishWebstoreEntity` | PortalId, LocaleId | Theme, CSS, logo, favicon |
| `ZnodePimCatalog` | PimCatalogId, CatalogName | Catalog lookup |

**Always filter by:** `PortalId` + `PimCatalogId` + `LocaleId` + `IsActive = 1`

---

## Transactional Tables (Znode_Entities — Used for writes + B2B config lookups)

| Table | Key Columns | When to use |
|---|---|---|
| `ZnodePortal` | PortalId, StoreName, StoreCode, IsBStore, ParentPortalId | Portal config, B2B/BStore detection |
| `ZnodePortalCatalog` | PortalId, PublishCatalogId | Portal→Catalog mapping |
| `ZnodePortalLocale` | PortalId, LocaleId, IsDefault | Portal→Locale mapping |
| `ZnodePortalProfile` | PortalId, ProfileId, IsDefaultAnonymousProfile, IsDefaultRegistedProfile | Portal→Profile mapping |
| `ZnodeProfile` | ProfileId, PimCatalogId, ProfileName | B2B Profile→Catalog link |
| `ZnodeAccount` | AccountId, CompanyName | B2B company accounts |
| `ZnodeAccountProfile` | AccountId, ProfileId, IsDefault | Account→Profile link |
| `ZnodeUserProfile` | UserId, ProfileId | User-level profile override |
| `ZnodeUser` | UserId, Email, AccountId | All users |
| `ZnodePriceList` | PriceListId, PriceListName | Price list metadata |
| `ZnodePriceListProfile` | PortalProfileId, PriceListId | Profile→PriceList |
| `ZnodePriceListAccount` | AccountId, PriceListId | Account-level price override |
| `ZnodePrice` | PriceListId, SKU, RetailPrice, SalePrice | Actual product prices |
| `ZnodeOmsCart` | CartId, AccountId, PortalId | Shopping carts |
| `ZnodeOmsOrder` | OmsOrderId, OrderNumber, AccountId, PortalId | Orders |

---

## Helper Views (Use these instead of manual joins)

| View | What it returns |
|---|---|
| `View_GetProfileCatalog` | Catalogs accessible from a given ProfileId |
| `View_AccountProfileList` | All profiles assigned to an AccountId |
| `View_GetAssociatedProfileToPriceList` | Price lists for a (PortalId, ProfileId) pair |
| `View_GetAssociatedPortalToPriceList` | All price lists for a PortalId |

---

## The "Wrong Data" Bug Checklist

If a service returns wrong/missing data, check these in order:

1. ❌ **Missing `PortalId` filter** — returning data from all portals
2. ❌ **Missing `PimCatalogId` filter** — returning products from wrong catalog
3. ❌ **Missing `LocaleId` filter** — returning wrong language content
4. ❌ **Missing `IsActive = 1` filter** — returning disabled/archived products
5. ❌ **Using wrong catalog** — using `ZnodePortalCatalog` catalog when should use `ZnodeProfile.PimCatalogId` for B2B account
6. ❌ **Ignoring `IsDefault` profile** — loading wrong profile's catalog for a B2B account
7. ❌ **Querying `Znode_Entities` for storefront reads** — should be `ZnodePublish_Entities`
