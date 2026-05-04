---
name: Shared Constants & Enums — Data Reference
description: Business enum values, status codes, attribute types, and SEO entity type codes used across all domains. DATA ONLY — coding patterns are in .claude/rules/. These values come from the Znode data model, not from v1/v2 code patterns.
type: project
---

> **Scope of this file:** What the values mean in the data model — not how to code with them.
> Use these values when filtering, comparing, or returning data.

---

## Product Types (`ZnodePimProduct.ProductType`)

| Value | Meaning |
|---|---|
| `Simple` | Single SKU product — most common |
| `Configurable` | Has selectable variants (size, color). Parent record + child SKU records linked by `ParentProductId` |
| `Grouped` | Multiple separate products sold as a set. Each has its own SKU |
| `Bundle` | Customer selects components. Final price = sum of selected parts |

**Configurable detail:** The parent product is what the customer browses. Child variants are discovered by loading `ZnodePublishProductEntity WHERE ParentProductId = @parentId`. Variant selection (e.g., "Size: M, Color: Blue") returns the specific child SKU.

---

## Publish States (`ZnodePimProduct.PublishStateId`)

| Value | Meaning | Storefront visible |
|---|---|---|
| `1` | Draft | No |
| `2` | Published | Yes |
| `3` | Archived | No |

When querying `ZnodePublish_Entities` directly: active/published records are already filtered. Still always add `IsActive = true` as a safety net.

---

## Order States (`ZnodeOmsOrderState`)

| Code | Meaning | Terminal? |
|---|---|---|
| `INPROGRESS` | Order created, payment not yet confirmed | No |
| `PENDING` | Payment captured, awaiting fulfillment | No |
| `PROCESSING` | Warehouse preparing shipment | No |
| `SHIPPED` | Tracking number assigned | No |
| `DELIVERED` | Delivery confirmed | **Yes — sealed** |
| `CANCELLED` | Order cancelled | **Yes — sealed** |
| `RETURNED` | Via RMA process | **Yes — sealed** |
| `ONHOLD` | Admin hold | No |

Valid transitions:
```
INPROGRESS → PENDING
PENDING    → PROCESSING | CANCELLED
PROCESSING → SHIPPED | CANCELLED
SHIPPED    → DELIVERED
```

Sealed states (`DELIVERED`, `CANCELLED`, `RETURNED`) accept no further transitions. Any mutation attempting a sealed-state transition is a business rule violation.

---

## Payment Status (`ZnodeOrderPayment.PaymentStatus`)

| Value | Meaning |
|---|---|
| `AUTHORIZED` | Payment held by gateway, not yet captured |
| `CAPTURED` | Payment complete — funds received |
| `DECLINED` | Rejected by payment gateway |
| `REFUNDED` | Funds returned to customer |
| `VOIDED` | Authorization cancelled before capture |
| `PENDING` | Awaiting gateway confirmation |
| `FAILED` | Technical failure during processing |

---

## Discount Types (`ZnodeOmsOrderDiscount.DiscountType`)

| Value | Meaning |
|---|---|
| `CSRDISCOUNT` | Manual discount applied by customer service representative |
| `VOUCHERNUMBER` | Gift voucher code redemption |
| `COUPONCODE` | Standard checkout coupon code |
| `PROMOCODE` | Promotional code (broader — auto-applied or entered) |

---

## Attribute Types (used in product attributes and global attributes)

| `AttributeTypeName` | How to interpret `AttributeValue` |
|---|---|
| `Text` | Plain string |
| `TextArea` | Multi-line string |
| `Number` | Decimal string — parse as `decimal` |
| `Date` | ISO 8601 date string |
| `Boolean` | String `"true"` or `"false"` |
| `Select` | One option code string — lookup display label in `SelectValues[]` |
| `MultiSelect` | Comma-separated codes: `"red,blue,green"` — split to get array |
| `MediaManager` | Filename only — prepend `MediaConfig.MediaServerUrl` for full URL |
| `TextEditor` | Raw HTML string |
| `Label` | Display-only — no editable value |

`MultiSelect` display: split value on `,`, then find each code in `SelectValues[]` to get the display label.

`MediaManager` display: `value = "hero.jpg"` → `fullUrl = MediaConfig.MediaServerUrl + "/hero.jpg"`.

---

## SEO Entity Types (`ZnodeCMSSEODetail.EntityType`)

| Value | Entity | `SEOCode` maps to |
|---|---|---|
| `Product` | Product | `Sku` |
| `Category` | Category | `CategoryCode` |
| `Brand` | Brand | `BrandCode` |
| `ContentPage` | CMS page | `PageCode` |
| `SubCategory` | Sub-category | `CategoryCode` |

Used when resolving a URL slug to an entity: find by `PortalId + SEOUrl`, then use `EntityType` to know which table to load from and `SEOCode` for the lookup key.

---

## Audit Fields (Present on Every Table)

Every entity table has these columns. They track who created/changed each record and when.

| Column | Type | Value |
|---|---|---|
| `CreatedBy` | int | UserId of the user who created the record |
| `CreatedDate` | datetime | UTC time of creation |
| `ModifiedBy` | int | UserId of last editor |
| `ModifiedDate` | datetime | UTC time of last edit |

These must be populated on every write operation (create or update). Use `DateTime.UtcNow` — never local time.

---

## Soft Delete Pattern (Universal Across All Tables)

No table uses hard deletes. Records are deactivated via `IsActive = false`.

| Query context | Filter |
|---|---|
| Storefront reads | Always `IsActive = true` |
| Admin list views | Show all, or allow toggle to show inactive |
| Admin lookups | Show all (inactive records may need editing) |

---

## Media URL Construction (Universal)

Image/file values in the database are **filenames only** — never full URLs.

| URL type | Formula |
|---|---|
| Full image | `MediaConfig.MediaServerUrl + "/" + filename` |
| Thumbnail | `MediaConfig.MediaServerThumbnailUrl + "/" + filename` |

`MediaConfig` values come from the `media-config` attribute group in `GlobalAttributeGroupsJson` on the portal. They are locale-independent.

---

## Portal Country Codes

Shipping addresses must be validated against `ZnodePortalCountry.CountryCode` for the portal. Standard ISO 3166-1 alpha-2 codes are used (e.g., `"US"`, `"GB"`, `"CA"`, `"DE"`).

---

## Locale Codes

Standard IETF BCP 47 tags. Look up `LocaleId` from `ZnodeLocale.Code` dynamically — never hardcode a numeric locale ID in code.

Common examples: `"en-US"`, `"en-GB"`, `"fr-FR"`, `"de-DE"`, `"es-ES"`, `"fr-CA"`, `"zh-CN"`

Always resolve the default locale for a portal from `ZnodePortalLocale.IsDefault = true` when no `localeId` argument is supplied.
