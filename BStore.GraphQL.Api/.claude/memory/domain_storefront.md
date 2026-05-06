---
name: Storefront & Portal Domain — Data & Workflow Reference
description: Tables, columns, business rules, and workflows for portal configuration, theme, global attributes, payment, shipping, tax, feature flags. DATA ONLY — coding patterns live in .claude/rules/. Never copy v1/v2 code style.
type: project
---

> **Scope of this file:** What data exists, how it relates, and what the business rules are.
> How to write EF Core queries, services, or resolvers is in `.claude/rules/`.

---

## Portal

### Where Data Lives

| Table | Key Columns | Purpose |
|---|---|---|
| `ZnodePortal` | PortalId, StoreName, StoreCode, IsBStore, ParentPortalId, IsActive, CompanyName | Portal master |
| `ZnodePortalCatalog` | PortalId, PublishCatalogId | Portal → catalog (PublishCatalogId = PimCatalogId) |
| `ZnodePortalLocale` | PortalId, LocaleId, IsDefault, IsActive | Supported languages |
| `ZnodeLocale` | LocaleId, Code ("en-US"), Name | Locale master |
| `ZnodePortalProfile` | PortalId, ProfileId, IsDefaultAnonymousProfile, IsDefaultRegistedProfile | Default profiles |
| `ZnodePublishPortalGlobalAttributeEntity` | PortalId, LocaleId, GlobalAttributeGroupsJson | Published portal settings |
| `ZnodePublishWebstoreEntity` | PortalId, LocaleId, WebstoreJson | Published theme/webstore config |

### Portal Types

| `IsBStore` | `ParentPortalId` | Type |
|---|---|---|
| `false` | `null` | B2C or B2B standalone portal |
| `true` | `null` | BStore root (multi-tenant platform) |
| `true` | non-null | BStore child (tenant store) |

### BStore-Specific Columns on `ZnodePortal`

| Column | Meaning |
|---|---|
| `IsBStoreLoginRequired` | Guest browsing disabled — must log in |
| `IsBStoreShowPricing` | Whether prices are visible to guests |
| `IsBStoreUsersSelfRegister` | Public self-registration allowed |
| `IsBStoreRequestPage` | Show access-request page for guests |
| `IsCatalogCustomized` | Tenant has its own catalog (not inherited from parent) |
| `BStoreDomainName` | Custom domain for this tenant store |
| `ParentPortalId` | Links to BStore root — inherit settings unless overridden |

**BStore catalog inheritance rule:**  
If `IsCatalogCustomized = false`, the child tenant uses the parent BStore's catalog from `ZnodePortalCatalog` on the parent. If `true`, the child has its own entry in `ZnodePortalCatalog`.

### Locale Resolution Order

```
1. Explicit localeId argument in query
2. ZnodePortalLocale.IsDefault = true for that portal
3. System fallback (always ensure at least one locale is present)
```

---

## Global Attributes (Portal Settings)

**All portal-level settings are stored as attribute groups, not as flat DB columns.** This allows the Znode admin to add new settings without schema changes.

### Where Data Lives

- Published: `ZnodePublishPortalGlobalAttributeEntity.GlobalAttributeGroupsJson` (read for storefront)
- Source: `ZnodePortalGlobalAttributeValue` → `ZnodeGlobalAttribute` (read by admin for editing)

### GlobalAttributeGroupsJson Structure

```json
[{
  "GlobalAttributeGroup": {
    "GroupCode": "store-info",
    "GroupName": "Store Information",
    "DisplayOrder": 1,
    "GlobalAttributes": [
      {
        "AttributeCode": "storeName",
        "AttributeName": "Store Name",
        "AttributeTypeName": "Text",
        "SingleAttributeValue": "My Store",
        "SelectValues": []
      }
    ]
  }
}]
```

### Standard Attribute Groups and Their Codes

**store-info**
```
storeName, storeTitle, companyName, storeEmail, storePhone, storeAddress
```

**social-media**
```
facebookUrl, twitterUrl, instagramUrl, linkedinUrl, youtubeUrl, pinterestUrl
```

**design-settings**
```
primaryColor, secondaryColor, accentColor, fontFamily, buttonStyle
```

**header-settings**
```
logoUrl, faviconUrl, headerBannerText, showTopBanner, headerBannerBgColor
```

**footer-settings**
```
footerCopyrightText, footerLogoUrl
```

**media-config**
```
mediaServerUrl, mediaServerThumbnailUrl
```
(These are locale-independent — never include locale in the cache key for media-config)

**feature-flags** — controls which features are active on this portal

| AttributeCode | Controls |
|---|---|
| `enableWishlist` | Wishlist button on products |
| `enableProductReviews` | Review form and star ratings |
| `enableGuestCheckout` | Checkout without account |
| `enableQuotes` | B2B quote workflow |
| `enableOrderApproval` | B2B order approval chain |
| `enableBudgetManagement` | B2B spending limits |
| `enableVoiceSearch` | Voice search UI |
| `enableBarcodeScanner` | Barcode scan input |
| `enableCMSPageSearch` | Include CMS pages in search results |
| `enableTypeahead` | Search autocomplete |
| `enableFacetedSearch` | Filter sidebar in search results |

**seo-defaults**
```
defaultMetaTitle, defaultMetaDescription, defaultMetaKeywords
```

**checkout-settings**
```
guestCheckoutEnabled, requirePhoneForCheckout, requireAccountForCheckout
```

**b2b-settings**
```
enableQuotes, enableOrderApproval, enableBudgetManagement, showPricingToGuests
```

---

## Theme

### Where Data Lives

| Table | Key Columns | Purpose |
|---|---|---|
| `ZnodeCMSPortalTheme` | PortalId, ThemeId, IsActive | Portal → theme |
| `ZnodeCMSTheme` | ThemeId, ThemeName, ThemeCode | Theme master |
| `ZnodePublishWebstoreEntity` | PortalId, LocaleId, WebstoreJson | Published theme details |

### WebstoreJson Contents
```
ThemeName, FaviconUrl, LogoUrl, PrimaryColor, SecondaryColor,
AccentColor, FontFamily, CustomCSS, HeaderLayout, FooterLayout, WebsiteTitle
```

---

## Payment Methods

### Where Data Lives

| Table | Key Columns | Purpose |
|---|---|---|
| `ZnodePortalPaymentSetting` | PortalId, PaymentSettingId, IsActive, DisplayOrder | Portal → payment method |
| `ZnodePaymentSetting` | PaymentSettingId, PaymentCode, PaymentName, GatewayName | Payment method master |

### Payment Codes

| Code | Method |
|---|---|
| `CREDITCARD` | Credit/debit card via gateway |
| `PURCHASEORDER` | B2B net terms |
| `CHECK` | Paper check |
| `ACH` | Bank transfer |
| `PAYPAL` | PayPal |
| `GOOGLEPAY` | Google Pay |
| `APPLEPAY` | Apple Pay |
| `COD` | Cash on delivery |

Rules:
- Only `IsActive = true` methods shown, ordered by `DisplayOrder`
- API never stores card numbers — payment processing goes directly through gateway
- B2B portals typically offer `PURCHASEORDER`

---

## Shipping

### Where Data Lives

| Table | Key Columns | Purpose |
|---|---|---|
| `ZnodePortalShipping` | PortalId, ShippingId, IsActive, DisplayOrder | Portal → shipping method |
| `ZnodeShipping` | ShippingId, ShippingName, ShippingCode, CarrierName | Shipping method master |
| `ZnodeShippingRate` | ShippingId, MinWeight, MaxWeight, Rate | Fallback rate table |
| `ZnodePortalCountry` | PortalId, CountryCode | Allowed shipping countries |

Shipping rates come from external provider when enabled. Fallback: weight-based rate from `ZnodeShippingRate`.

Destination country must be in `ZnodePortalCountry` for the portal — validate before allowing address save.

---

## Tax

### Where Data Lives

| Table | Key Columns | Purpose |
|---|---|---|
| `ZnodePortalTaxClass` | PortalId, TaxClassId | Portal → tax class |
| `ZnodeTaxClass` | TaxClassId, TaxClassName, TaxRate | Tax class |
| `ZnodeTaxRule` | TaxClassId, StateCode, PostalCodeRange, TaxRate | Rate by location |

Tax source: external Tax provider when `ProviderRegistry["Tax"]` is enabled.  
Fallback: `ZnodeTaxRule` lookup by destination state + postal code range.

Tax-exempt accounts: `ZnodeAccount.IsTaxExempt = true` — no tax applied to their orders.

---

## websiteEntry Cached vs Uncached Split

This is the core data scoping decision for the `websiteEntry` query:

| Data group | Changes when | Cache |
|---|---|---|
| Portal identity, StoreName, StoreCode | Portal config change + publish | L1, 120s |
| GlobalAttributes (all groups) | Portal attribute change + publish | L1, 120s |
| Theme | Theme change + publish | L1, 120s |
| Navigation tree | Category/catalog publish | L1, 30min |
| Locales | Portal locale change | L1, 120s |
| FeatureFlags | Attribute change + publish | L1, 120s |
| MediaConfig | Portal setting change | L1 only (never Redis — rarely changes) |
| `userContext` | Every request — per-user | **NEVER cached** |
| `b2bContext` | Every request — per-user+account | **NEVER cached** |

`userContext` contains: `isLoggedIn`, `cartItemCount`, `userId`, `accountId`  
`b2bContext` contains: `isBStore`, `accountName`, `profileId`, `catalogId`
