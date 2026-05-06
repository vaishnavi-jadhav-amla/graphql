---
name: websiteEntry Query — Implementation Details
description: The primary store entry query that replaces 10-15 REST calls in one request. Attribute-based, split cached/uncached, profile-aware navigation.
type: project
---

## Purpose

`websiteEntry(portalId, localeId?, profileId?)` is the single entry-point query for all storefront page loads. It returns everything the frontend needs for the first render.

## GraphQL Signature

```graphql
# On /graphql/storefront
query {
  websiteEntry(portalId: 1, localeId: 1, profileId: null) {
    portal { portalId portalName storeCode companyName isBStore isActive }
    globalAttributes {
      groupCode attributeGroupName displayOrder
      attributes {
        attributeCode attributeName attributeTypeName
        attributeValue
        selectValues { value code displayOrder }
      }
    }
    theme { themeName faviconUrl primaryColor secondaryColor websiteTitle customCss }
    navigation { categoryId name seoUrl displayOrder children { ... } }
    locales { localeId code name isDefault }
    featureFlags { featureName isEnabled }
    mediaConfig { mediaServerUrl mediaServerThumbnailUrl }
    userContext { isLoggedIn cartItemCount userId accountId }
    b2bContext { isBStore accountName profileId catalogId }
  }
}
```

**HotChocolate strips "Get" prefix** → method `GetWebsiteEntry` becomes field `websiteEntry` in the schema.

## Two-Tier Response Pattern

| Field group | Cache | TTL | Why |
|---|---|---|---|
| `portal`, `globalAttributes`, `theme`, `navigation`, `locales`, `featureFlags`, `mediaConfig` | L1 cache | 120s | Changes only on publish — safe to cache |
| `userContext`, `b2bContext` | **NEVER** cached | — | Per-user, per-request — caching causes cross-user leakage |

## Key Design Decision: Attribute-Based (ADR-001)

`globalAttributes` surfaces ALL portal attributes as generic groups — not hardcoded flat fields. When an admin adds a new attribute in the Znode admin portal (e.g., "twitterUrl"), it appears automatically in the response without code changes.

Frontend reads attributes by code:
```js
globalAttributes.flatMap(g => g.attributes).find(a => a.attributeCode === "storeName")?.attributeValue
```

## Files

| File | Role |
|---|---|
| `Types/Storefront/WebsiteEntryTypes.cs` | All POCOs: `WebsiteEntryType`, `PortalIdentityType`, `GlobalAttributeGroupType`, `GlobalAttributeValueType`, `AttributeSelectOptionType`, `StoreThemeType`, `PortalFeatureType`, `MediaConfigType`, `UserContextType`, `B2BContextType` |
| `Services/Storefront/IWebsiteEntryService.cs` | Interface: `GetPortalEntryAsync`, `GetUserContextAsync`, `GetB2BContextAsync` |
| `Services/Storefront/WebsiteEntryService.cs` | Implementation: cached portal data + uncached user/B2B data |
| `Queries/Storefront/WebsiteEntryQueries.cs` | `[ExtendObjectType(typeof(StorefrontQuery))]` — calls service, merges portal + user data |

## [GraphQLName] Fix — Critical Pattern

HotChocolate acronym bug: `B2BContext` property becomes `b2BContext` in schema (wrong). Fixed with:

```csharp
[GraphQLName("b2bContext")]
public B2BContextType B2BContext { get; set; } = new();
```

This is the canonical example of ADR-006. All acronym properties need `[GraphQLName]`.

## JSON Deserialization from Published Tables

`GlobalAttributeGroups` is a JSON column on `ZnodePublishPortalGlobalAttributeEntity`. Service deserializes it into:

```json
[{
  "GlobalAttributeGroup": {
    "GlobalAttributeGroupId": 1,
    "GroupCode": "basic-info",
    "GlobalAttributes": [
      { "AttributeCode": "storeName", "SingleAttributeValue": "My Store", ... }
    ]
  }
}]
```

Private models in `WebsiteEntryService.cs` handle this structure. **Never expose these internal models as GraphQL types.**

## Profile-Aware Navigation

When `profileId` is passed:
- Look up the profile's assigned catalog via `Znode_Entities`
- Call `ICategoryService.GetNavigationTreeAsync(portalId, profileId)` instead of the default
- Cache key: `nav:tree:{portalId}:{profileId}` (vs `nav:tree:{portalId}:0` for default)

## Cache Keys Used

```
portal:{id}:identity              → L1 only
portal:{id}:locale:{l}:attributes → L1 (120s)
portal:{id}:nav:tree              → L1 (120s)  [delegated to CategoryService]
portal:{id}:nav:tree:{profileId}  → L1 (120s)  [profile-specific]
portal:{id}:features              → L1 (120s)
portal:{id}:theme                 → L1 (120s)
```

## Diagnoser

`WebsiteEntryDiagnoser` runs when the result is empty (pending implementation):
- Portal exists check
- Portal active check
- Recent publish check
- Catalog assigned check
- Published attributes check
