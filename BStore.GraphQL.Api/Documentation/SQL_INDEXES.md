# Required SQL Indexes (ADR-013)

The GraphQL service relies on the indexes below being present on `Znode_Entities` and
`ZnodePublish_Entities`. They are *not* created by the API — DBA tooling must apply them
before the storefront load test runs.

## ZnodePublishProductDetail

| Index | Columns | Purpose |
| ----- | ------- | ------- |
| `IX_ZPPD_PublishProductId`  | `PublishProductId, PublishProductInfoId DESC` | `productList`, DataLoader batch fetch by id |
| `IX_ZPPD_SKULower`          | `SKULower`                                    | `productSearch` SKU lookups |
| `IX_ZPPD_ProductName`       | `ProductName`                                 | `productSearch` name lookups |

## ZnodePublishCategoryDetail

| Index | Columns | Purpose |
| ----- | ------- | ------- |
| `IX_ZPCD_CategoryCode`      | `CategoryCode, IsActive`                      | `productCategories` slug lookup |
| `IX_ZPCD_PublishCategoryId` | `PublishCategoryId`                           | Materialised path tree join |

## ZnodePublishCategoryProduct

| Index | Columns | Purpose |
| ----- | ------- | ------- |
| `IX_ZPCP_PublishCategoryId` | `PublishCategoryId, PublishProductId`         | `productsByCategory` |

## ZnodePortals

| Index | Columns | Purpose |
| ----- | ------- | ------- |
| `IX_ZP_IsBStore_IsActive`   | `IsBStore, IsActive, ParentPortalId`          | `bStoreList`, queryable filter |
| `IX_ZP_StoreCode`           | `StoreCode`                                   | Storefront resolution by code |

## ZnodePublishPortalGlobalAttributeEntity

| Index | Columns | Purpose |
| ----- | ------- | ------- |
| `IX_ZPPGAE_PortalId_VersionId` | `PortalId, VersionId DESC`                 | Latest attribute snapshot per portal |

## ZnodePublishSeoEntity

| Index | Columns | Purpose |
| ----- | ------- | ------- |
| `IX_ZPSE_PortalId_SeoUrl`   | `PortalId, SEOUrl`                            | `websiteEntry` page resolution |
