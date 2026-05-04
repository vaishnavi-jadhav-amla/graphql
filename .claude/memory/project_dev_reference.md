---
name: Dev Environment Reference — Test Data, IDs, Endpoints
description: Known dev environment portal IDs, test accounts, JWT tokens, endpoints. Use this when writing test queries or debugging against the dev DB.
type: project
---

## Dev Database

- **Server:** `190.190.0.194`
- **Database:** `Magicians`
- **Type:** Dev-only — not production data
- **Access:** Connection string in `appsettings.json` under `ConnectionStrings.ZnodePublishDb`

## GraphQL Endpoints (Dev)

| Schema | URL |
|---|---|
| Storefront | `http://localhost:5000/graphql/storefront` |
| Admin | `http://localhost:5000/graphql/admin` |
| Banana Cake Pop | `http://localhost:5000/graphql` |

## Starting the Server

```bash
cd D:\Base_Code\Znode.Engine.GraphQL
dotnet run
```

Or use the launch config: `graphql-server` in `.claude/launch.json`

## Test Query — Health Check (no auth required)

```graphql
# Storefront
{ health }

# Admin
{ health }
```

## Test Query — websiteEntry (requires auth)

```graphql
query {
  websiteEntry(portalId: 1, localeId: 1) {
    portal { portalId portalName storeCode }
    globalAttributes {
      groupCode
      attributes { attributeCode attributeValue }
    }
    theme { themeName }
    userContext { isLoggedIn }
    b2bContext { isBStore }
  }
}
```

## Dev Portal IDs

> ⚠️ Update this table as you discover portal IDs in the Magicians DB. Run this query to find them:
> `SELECT PortalId, StoreName, StoreCode, IsBStore FROM ZnodePortal ORDER BY PortalId`

| PortalId | StoreName | Type | Notes |
|---|---|---|---|
| (run query to fill in) | | | |

## How to Get a JWT Token for Testing

```bash
# Replace email/password with dev credentials
curl -X POST http://localhost:5000/graphql/storefront \
  -H "Content-Type: application/json" \
  -d '{"query":"mutation { login(email: \"admin@example.com\", password: \"password\") { accessToken } }"}'
```

## How to Test with Admin JWT

```bash
curl -X POST http://localhost:5000/graphql/admin \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer {TOKEN}" \
  -d '{"query":"{ providers { name isEnabled } }"}'
```

## How to Test with Debug Tracing

```bash
curl -X POST http://localhost:5000/graphql/storefront \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer {ADMIN_TOKEN}" \
  -H "X-Debug-Level: trace" \
  -d '{"query":"{ websiteEntry(portalId: 1) { portal { portalId } } }"}'
```

Response will include `extensions.timings`, `extensions.dataSources`, `extensions.correlationId`.

## Common Portal Lookup Queries (Run in SSMS or via db-expert agent)

```sql
-- All portals
SELECT PortalId, StoreName, StoreCode, IsBStore, ParentPortalId FROM ZnodePortal ORDER BY PortalId

-- Portal → Catalog assignment
SELECT pc.PortalId, p.StoreName, pc.PublishCatalogId, c.CatalogName
FROM ZnodePortalCatalog pc
JOIN ZnodePortal p ON pc.PortalId = p.PortalId
JOIN ZnodePimCatalog c ON pc.PublishCatalogId = c.PimCatalogId

-- Portal → Locale assignment
SELECT pl.PortalId, p.StoreName, l.Code, l.Name, pl.IsDefault
FROM ZnodePortalLocale pl
JOIN ZnodePortal p ON pl.PortalId = p.PortalId
JOIN ZnodeLocale l ON pl.LocaleId = l.LocaleId

-- B2B Accounts and their profiles
SELECT a.AccountId, a.CompanyName, pr.ProfileName, pr.PimCatalogId, ap.IsDefault
FROM ZnodeAccount a
JOIN ZnodeAccountProfile ap ON a.AccountId = ap.AccountId
JOIN ZnodeProfile pr ON ap.ProfileId = pr.ProfileId

-- Published products for a portal
SELECT TOP 10 ZnodeProductId, Sku, Name, PortalId, PimCatalogId, LocaleId
FROM ZnodePublishProductEntity
WHERE PortalId = 1 AND IsActive = 1
ORDER BY ZnodeProductId DESC
```
