---
name: CMS Domain — Data & Workflow Reference
description: Tables, columns, business rules, and workflows for Page Builder, Content Pages, SEO, Widgets, URL Redirects. DATA ONLY — coding patterns live in .claude/rules/. Never copy v1/v2 code style.
type: project
---

> **Scope of this file:** What data exists, how it relates, and what the business rules are.
> How to write EF Core queries, services, or resolvers is in `.claude/rules/`.

---

## Page Builder

### Where Data Lives

| Table | Key Columns | Purpose |
|---|---|---|
| `ZnodeCMSContentPage` | CMSContentPageId, PortalId, IsActive, CMSContentPageGroupId, PublishStateId | Page definition |
| `ZnodeCMSContentPagesLocale` | CMSContentPageId, LocaleId, PageTitle, BodyContent, SEOUrl | Locale page content |
| `ZnodeCMSContentContainer` | CMSContentContainerId, CMSContentPageId, ContainerKey, DisplayOrder | Layout containers |
| `ZnodeCMSContentWidget` | CMSContentWidgetId, CMSContentContainerId, WidgetTypeId, DisplayOrder, IsActive | Widget instances |
| `ZnodeCMSContentPageGroup` | CMSContentPageGroupId, PortalId, GroupCode, GroupName | Page collections |

### Publish States

| `PublishStateId` | Meaning | Visible on storefront |
|---|---|---|
| 1 | Draft | No — admin preview only |
| 2 | Published | Yes |
| 3 | Archived | No |

**Storefront always filters `PublishStateId = 2`.** Admin can view all states.

### Page Groups (Collections)

Pages are organized into named groups. `GroupCode` is the stable identifier:

| GroupCode | Typical use |
|---|---|
| `landing-pages` | Marketing/promotional pages |
| `blog` | Blog/article content |
| `legal` | Terms, Privacy Policy, Returns |
| `support` | FAQ, Help, Contact |
| `product-guides` | How-to guides |

### Slug → Page Workflow

```
Client requests page at slug "/about-us"
  ↓
ZnodeCMSSEODetail
  WHERE PortalId = @portalId AND SEOUrl = "/about-us" AND EntityType = 'ContentPage'
  → EntityId = CMSContentPageId
  ↓
ZnodeCMSContentPage
  WHERE CMSContentPageId = @id AND PortalId = @portalId AND PublishStateId = 2
  ↓
ZnodeCMSContentPagesLocale
  WHERE CMSContentPageId = @id AND LocaleId = @localeId
  ↓
ZnodeCMSContentContainer (ordered by DisplayOrder)
  ↓
ZnodeCMSContentWidget per container (ordered by DisplayOrder)
  ↓ [load widget-specific config for each widget type separately]
Return assembled page
```

---

## Widgets

### Widget Types

| Widget | What it does | Type-specific table |
|---|---|---|
| `Text` / `HTML` | Static rich-text block | `ZnodeCMSTextWidget` |
| `Product` | Featured product(s) | SKUs stored in widget config |
| `Category` | Category banner or listing | CategoryId stored in widget config |
| `Slider` | Image slideshow/carousel | `ZnodeCMSSlider` + `ZnodeCMSSliderBanner` |
| `Banner` | Single promotional image | `ZnodeCMSBanner` |
| `Search` | Search input box | No extra table |
| `Form` | Contact/lead form | `ZnodeCMSForm` |
| `Video` | Embedded video | `ZnodeCMSVideoWidget` |
| `Voucher` | Voucher code entry | No extra table |
| `Custom` | Client-defined widget | Varies |

### Slider/Carousel Structure

| Table | Key Columns | Purpose |
|---|---|---|
| `ZnodeCMSSlider` | SliderId, AutoPlay, Speed, TransitionType | Slider config |
| `ZnodeCMSSliderBanner` | SliderId, ImageUrl, LinkUrl, AltText, DisplayOrder, IsActive | Individual slides |

Slides ordered by `DisplayOrder`. `ImageUrl` is a filename — prepend `MediaConfig.MediaServerUrl`.

### Widget Loading Rule

Only load a widget type's specific table if the client has selected those fields. Each widget type has a different data shape. In the GraphQL schema this is modelled as a union type or interface — each widget type returns a different concrete type.

---

## SEO

### Where Data Lives

| Table | Key Columns | Purpose |
|---|---|---|
| `ZnodeCMSSEODetail` | EntityId, EntityType, PortalId, SEOUrl, MetaTitle, MetaDescription, MetaKeywords, IsActive | SEO per entity |
| `ZnodeCMSSEODetailLocale` | CMSSEODetailId, LocaleId, MetaTitle, MetaDescription, MetaKeywords, SEOUrl | Locale overrides |
| `ZnodeCMSUrlRedirect` | PortalId, RedirectFrom, RedirectTo, RedirectStatusCode, IsActive | URL redirects |

### EntityType Values

| EntityType | Entity table | SEOCode maps to |
|---|---|---|
| `Product` | `ZnodePublishProductEntity` | SKU |
| `Category` | `ZnodePublishCategoryEntity` | CategoryCode |
| `Brand` | `ZnodeBrandDetail` | BrandCode |
| `ContentPage` | `ZnodeCMSContentPage` | PageCode |

### SEO Fallback Chain

```
1. ZnodeCMSSEODetailLocale for this entity + localeId  (locale-specific override)
2. ZnodeCMSSEODetail base record for this entity         (fallback)
3. Generated defaults:
   Product  → MetaTitle = "{ProductName} | {StoreName}"
   Category → MetaTitle = "{CategoryName} | {StoreName}"
   Page     → MetaTitle = "{PageTitle} | {StoreName}"
```

### URL Redirect Rules

Before returning 404:
1. Check `ZnodeCMSUrlRedirect WHERE PortalId = @id AND RedirectFrom = @slug AND IsActive = true`
2. If found: return redirect instruction with `RedirectTo` and `RedirectStatusCode`
3. `301` = permanent (browser caches), `302` = temporary

The GraphQL response should include redirect info in a structured field so the Next.js BFF can issue the correct HTTP redirect to the browser.

---

## Highlights (Product Badges)

### Where Data Lives

| Table | Key Columns | Purpose |
|---|---|---|
| `ZnodeHighlight` | HighlightId, HighlightCode, HighlightColor, HighlightImage | Badge definition |
| `ZnodeHighlightLocale` | HighlightId, LocaleId, HighlightName | Locale label |
| `ZnodePimProductHighlight` | PimProductId, HighlightId | Product → badges |

### Standard Highlight Codes

| Code | Meaning |
|---|---|
| `NEW` | Recently added product |
| `SALE` | Currently on sale price |
| `BESTSELLER` | Top-selling |
| `FEATURED` | Manually featured |
| `OUTOFSTOCK` | No stock available |
| `BACKORDER` | Back-orderable |

Highlights are included in `PublishProductJson` for storefront reads — no separate query needed.

---

## Media (All Domains)

**Every image/file value stored in the database is a filename only — never a full URL.**

Construct full URL: `{MediaConfig.MediaServerUrl}/{filename}`  
Thumbnail: `{MediaConfig.MediaServerThumbnailUrl}/{filename}`

`MediaConfig` values come from the `media-config` attribute group on the portal global attributes.

These values are **locale-independent** — do not include `LocaleId` in the cache key for media config.
