---
paths:
  - "**/Services/**/*.cs"
  - "**/*DbContext*.cs"
  - "**/*Entity*.cs"
---

# Database & EF Core Rules

## Which DB to Query

| Data type | DbContext | When |
|---|---|---|
| Storefront reads (products, categories, pages, portal config) | `ZnodePublish_Entities` | Always for reads |
| Writes (orders, cart, account, address) | `Znode_Entities` | Always for writes |
| B2B config lookups (profile→catalog, account→profile) | `Znode_Entities` | When resolving B2B context |
| Pricing (fallback only) | `Znode_Entities` → `ZnodePrice` | When external Pricing provider is disabled |

**Never mix reads and writes in the same DbContext transaction.**

## Mandatory Filters (Missing Any = Wrong Data or Data Leak)

Every query against published tables must include ALL applicable filters:

```csharp
// Standard storefront product query — all 4 filters required
await _publishDb.ZnodePublishProductEntities
    .AsNoTracking()
    .Where(p =>
        p.PortalId == portalId &&         // ← REQUIRED: multi-tenant isolation
        p.PimCatalogId == catalogId &&    // ← REQUIRED: catalog scoping
        p.LocaleId == localeId &&         // ← REQUIRED: language
        p.IsActive == true)               // ← REQUIRED: exclude archived/disabled
    .ToListAsync(ct);
```

### Getting `catalogId` — it depends on the user type:

```csharp
// B2C: catalog comes from portal assignment
var catalogId = await _db.ZnodePortalCatalogs
    .Where(pc => pc.PortalId == portalId)
    .Select(pc => pc.PublishCatalogId)
    .FirstOrDefaultAsync(ct);

// B2B with profileId: catalog comes from profile
var catalogId = await _db.ZnodeProfiles
    .Where(pr => pr.ProfileId == profileId)
    .Select(pr => pr.PimCatalogId)
    .FirstOrDefaultAsync(ct);

// B2B without profileId: use account's default profile
var catalogId = await _db.ZnodeAccountProfiles
    .Where(ap => ap.AccountId == accountId && ap.IsDefault == true)
    .Join(_db.ZnodeProfiles, ap => ap.ProfileId, pr => pr.ProfileId, (_, pr) => pr.PimCatalogId)
    .FirstOrDefaultAsync(ct);

// Shortcut: use View_GetProfileCatalog view for profile→catalog
```

## EF Core Query Rules

- **Always `AsNoTracking()`** on all read queries — change tracker is never needed for storefront reads.
- **Always `Select(...)` to project** — never fetch full entities when only a few columns are needed.
- **Use `FirstOrDefaultAsync`** not `SingleOrDefaultAsync` for SEO URL lookups (duplicates can exist).
- **Never hand-edit EF Core migrations or scaffolded entity classes.**
- **Never create new migrations** without explicit user instruction.
- **Use `AsSplitQuery()`** when joining multiple collections in one query (avoids Cartesian explosion).

## B2B Context Resolution (When accountId or profileId is present)

Use this sequence to determine the correct catalog for a B2B user:

1. If `profileId` is explicitly provided → `ZnodeProfile.PimCatalogId` for that profile
2. If `accountId` is provided (no profileId) → `ZnodeAccountProfile` where `IsDefault=true` → `ZnodeProfile.PimCatalogId`
3. If neither → fall back to portal default catalog from `ZnodePortalCatalog`

**Use views:** `View_GetProfileCatalog`, `View_AccountProfileList` to simplify joins.

## Security Rules

- **Never expose connection strings, DB credentials, or SQL error messages** to GraphQL responses.
- **Always sanitize exceptions through `ZnodeErrorFilter`** before they reach the client.
- **Throw `CrossTenantAccessException`** if the resolved portal/account doesn't match the JWT claims.

## Performance Rules

- `AsNoTracking()` is not optional — always add it.
- Use `Select(...)` projection to avoid loading unused columns, especially the large JSON columns (`PublishProductJson`, `GlobalAttributeGroupsJson`).
- For large list queries: use cursor-based pagination (see `big-data-queries.md`).
- For batch lookups (DataLoaders): use `Where(x => ids.Contains(x.Id))` — one query for all IDs.

## Reference: Key Table Relationships

See `project_business_data_model.md` in memory for the full table chain diagram.

Quick reference:
- Portal → Catalog: `ZnodePortalCatalog` (PortalId → PublishCatalogId = PimCatalogId)
- Portal → Locale: `ZnodePortalLocale` (PortalId → LocaleId, IsDefault)
- Account → Profile: `ZnodeAccountProfile` (AccountId → ProfileId, IsDefault)
- Profile → Catalog: `ZnodeProfile` (ProfileId → PimCatalogId)
- Portal + Profile → PriceList: `ZnodePortalProfile` → `ZnodePriceListProfile` → `ZnodePriceList`
