---
name: PIM Domain — Data & Workflow Reference
description: Tables, columns, relationships, business rules, and workflows for Products, Categories, Brands. DATA ONLY — coding patterns live in .claude/rules/. Never copy v1/v2 code style.
type: project
---

> **Scope of this file:** What data exists, how it relates, and what the business rules are.
> How to write EF Core queries, services, or resolvers is in `.claude/rules/`.

---

## Products

### Where Data Lives

| Purpose | Storefront table (read) | Admin/write table |
|---|---|---|
| Product records | `ZnodePublishProductEntity` | `ZnodePimProduct` |
| Attributes (EAV) | **Inside `PublishProductJson` column** | `ZnodePimAttributeValue` + locale table |
| Images | `PublishProductJson` (media array) | `ZnodePimProductImage` |
| SEO routing | `ZnodePublishSEOEntity` | `ZnodeCMSSEODetail` |
| Category-product link | `ZnodePublishCategoryProductEntity` | `ZnodePimCategoryProduct` |

**Key rule:** Storefront always reads from `ZnodePublish_Entities`. The EAV tables (`ZnodePimAttributeValue`) are for admin editing — never for storefront queries.

### Key Columns — `ZnodePublishProductEntity`

| Column | Type | Meaning |
|---|---|---|
| `ZnodeProductId` | int | Unique product ID |
| `Sku` | string | Stock keeping unit — unique per portal+catalog |
| `PortalId` | int | Which store |
| `PimCatalogId` | int | Which catalog |
| `LocaleId` | int | Which language |
| `IsActive` | bool | False = hidden from storefront |
| `PublishProductJson` | nvarchar(max) | All attributes, prices, media in one JSON blob |
| `ProductType` | string | "Simple" / "Configurable" / "Grouped" / "Bundle" |
| `ParentProductId` | int? | For configurable variants: links to parent SKU |
| `DisplayOrder` | int | Sort order within category |

### `PublishProductJson` — What's Inside

The JSON blob contains everything about the product as published. Key paths:

```
Attributes[]           → all attribute key-value pairs (attributeCode, attributeValue, attributeType)
Pricing.RetailPrice    → standard price
Pricing.SalePrice      → discounted price (null if no sale)
Pricing.TierPrices[]   → volume discount tiers (minQty, price)
Media[]                → images (imageUrl, thumbnailUrl, isDefault, displayOrder)
Highlights[]           → badges (NEW, SALE, BESTSELLER, etc.)
Seo.MetaTitle          → product SEO title
Seo.MetaDescription    → product SEO description
Seo.SEOUrl             → product URL slug
```

**Load this column only when the client requests attributes, specs, media, pricing — not for name/SKU-only queries.** (ADR-012)

### Product Types — What They Mean

| Type | What it means |
|---|---|
| `Simple` | Single SKU, no variants — most products |
| `Configurable` | Has variants (size/color/etc.) — parent record + child SKUs linked via `ParentProductId` |
| `Grouped` | Multiple separate products sold as a set — each has its own SKU |
| `Bundle` | Customer picks components from options — price is sum of selected parts |

For Configurable: the parent product is the display entity. Child variants are selected by the user (e.g., "Size: M"). Child records have `ParentProductId = parent.ZnodeProductId`.

### Inventory Rules

| `OutOfStockOptions` value | Storefront behavior |
|---|---|
| `AllowBackOrdering` | Add to cart allowed, shows "Ships in X days" |
| `DisableAddToCart` | Add to cart button disabled |
| `HideProductOnFrontEnd` | Product not shown at all |

`MinimumQuantity` and `MaximumQuantity` from `PublishProductJson` must be validated at Add-to-Cart time, not just displayed.

### Pricing Resolution (in order — first match wins)

1. `ZnodePriceListUser` — user-specific price list (highest priority)
2. `ZnodePriceListAccount` — account-level price list
3. `ZnodePriceListProfile` via `ZnodePortalProfile` — profile price list
4. Default portal price list (lowest priority)

Tier prices (volume discounts): `ZnodeTierPrice` (PriceListId, MinQty → Price per unit)

### SEO URL → Product Workflow

```
Incoming slug: "/mens/shirts/blue-oxford"
  ↓
ZnodePublishSEOEntity
  WHERE PortalId = @portalId AND SEOURL = @slug AND IsActive = true
  → SEOTypeId = 1 (Product), SEOCode = product SKU
  ↓
ZnodePublishProductEntity
  WHERE PortalId = @portalId AND Sku = @seoCode AND IsActive = true
```

`SEOTypeId` values: 1=Product, 2=Category, 3=Brand, 4=ContentPage

### Default Attribute Codes (Always Present in `PublishProductJson`)

These attribute codes exist on every product and must be supported:
```
SKU, ProductName, Description, ShortDescription, OutOfStockOptions,
ProductType, TypicalLeadTime, MaximumQuantity, MinimumQuantity,
AllowBackOrdering, SEOTitle, SEOKeywords, SEODescription
```

New attribute codes can be added in the Znode admin without code changes. The API must return them generically via the `Attributes[]` array.

---

## Categories

### Where Data Lives

| Purpose | Storefront table | Admin/write table |
|---|---|---|
| Category records | `ZnodePublishCategoryEntity` | `ZnodePimCategory` |
| Products in category | `ZnodePublishCategoryProductEntity` | `ZnodePimCategoryProduct` |
| Catalog assignment | within `ZnodePublishCategoryEntity` | `ZnodePimCategoryHierarchy` |

### Key Columns — `ZnodePublishCategoryEntity`

| Column | Type | Meaning |
|---|---|---|
| `CategoryId` | int | Unique category ID |
| `PortalId` | int | Which store |
| `PimCatalogId` | int | Which catalog |
| `LocaleId` | int | Which language |
| `CategoryName` | string | Display name |
| `CategoryPath` | string | Materialized path: `/1/45/289/` |
| `Level` | int | Depth in tree: 1=root, 2=sub, 3=sub-sub |
| `ParentCategoryId` | int? | Direct parent (null = root) |
| `DisplayOrder` | int | Sort order within parent |
| `IsActive` | bool | Hidden when false |
| `CategoryJson` | nvarchar(max) | Attributes, SEO, media for category |

### Tree Navigation

The `CategoryPath` column is the key to all tree queries:

| Operation | How |
|---|---|
| Get all children of `/1/45/` | `WHERE CategoryPath LIKE '/1/45/%'` — single index scan |
| Get mega-menu (3 levels) | `WHERE PortalId = @id AND PimCatalogId = @cid AND Level <= 3 ORDER BY Level, DisplayOrder` |
| Get root categories | `WHERE ParentCategoryId IS NULL AND PortalId = @id AND PimCatalogId = @cid` |
| Get breadcrumb ancestors | Split `CategoryPath` on `/`, look up each ID |

**No recursive CTEs.** The materialized path makes all of these flat indexed lookups.

### B2B Profile-Scoped Navigation

When a B2B user with a specific profile is browsing:
- Their catalog = `ZnodeProfile.PimCatalogId` for their active profile
- Navigation tree must filter `PimCatalogId` to their profile's catalog — not the portal's default catalog
- Cache key: `portal:{id}:nav:tree:{profileId}` vs `portal:{id}:nav:tree:0` for default/B2C

---

## Brands

### Where Data Lives

| Table | Key Columns | Purpose |
|---|---|---|
| `ZnodeBrandDetail` | BrandId, BrandName, IsActive, Description, LogoName | Brand master |
| `ZnodeBrandPortal` | BrandId, PortalId | Which portals show this brand |
| `ZnodeBrandProduct` | BrandId, PimProductId | Which products belong to brand |
| `ZnodeCMSSEODetail` | EntityId=BrandId, EntityType='Brand', SEOUrl | Brand SEO |

### Rules

- Only brands in `ZnodeBrandPortal` for the portal are shown (`PortalId` filter required)
- `LogoName` is a filename only — construct full URL via `MediaConfig.MediaServerUrl + "/" + LogoName`
- `IsActive = true` required

---

## Highlights (Product Badges)

| Table | Key Columns | Purpose |
|---|---|---|
| `ZnodeHighlight` | HighlightId, HighlightCode, HighlightColor | Badge definition |
| `ZnodeHighlightLocale` | HighlightId, LocaleId, HighlightName | Locale-specific label |
| `ZnodePimProductHighlight` | PimProductId, HighlightId | Product → badges |

Common `HighlightCode` values: `NEW`, `SALE`, `BESTSELLER`, `FEATURED`, `OUTOFSTOCK`, `BACKORDER`

Highlights are included in `PublishProductJson` as an array — no separate query needed for storefront reads.

---

## Attribute Type Meanings

These define how the `attributeValue` field should be interpreted by the frontend:

| AttributeTypeName | Value format |
|---|---|
| `Text` | Plain string |
| `TextArea` | Multi-line string |
| `Number` | Decimal string — parse as `decimal` |
| `Date` | ISO 8601 date string |
| `Boolean` | `"true"` or `"false"` string |
| `Select` | One option code string |
| `MultiSelect` | Comma-separated option codes: `"red,blue,green"` |
| `MediaManager` | Filename only — prepend `MediaConfig.MediaServerUrl` |
| `TextEditor` | Raw HTML string |

`MultiSelect` values must be split on `,` to get the array. Both the codes and their display labels live in `SelectValues[]` on the attribute.

---

## Publish Flow (How Data Gets to `ZnodePublish_Entities`)

Understanding this prevents "why is my change not showing?" bugs:

```
Admin edits product in Znode Admin portal
  ↓ saves to ZnodePimProduct + ZnodePimAttributeValue (Znode_Entities)
  ↓ admin clicks "Publish"
  ↓ Znode publish engine runs
  ↓ denormalizes EAV into PublishProductJson
  ↓ writes to ZnodePublishProductEntity (ZnodePublish_Entities)
  ↓ writes SEO routes to ZnodePublishSEOEntity
  ↓ cache invalidation event fired
```

**Storefront never sees unpublished edits.** If a product change is not showing, check publish status first.
