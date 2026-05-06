using BStore.GraphQL.Api.GraphQL.Queries;
using BStore.GraphQL.Api.GraphQL.Types;
using HotChocolate.Data;
using HotChocolate.Types;
using Microsoft.EntityFrameworkCore;
using Znode.Libraries.Data.ZnodeEntity;

namespace BStore.GraphQL.Api.GraphQL.Resolvers;

/// <summary>
/// GraphQL-native <see cref="IQueryable{T}"/> fields with projection/filter/sort over znode10 EF entities.
/// Use alongside imperative fields on <see cref="BStoreQuery"/> for the same underlying data.
/// </summary>
[ExtendObjectType(typeof(BStoreQuery))]
public sealed class BStoreQueryableResolvers
{
    [GraphQLDescription("Queryable B-store portals (ZnodePortals.IsBStore). Use where/order in GraphQL.")]
    [UseProjection]
    [UseFiltering]
    [UseSorting]
    public IQueryable<BStorePortalQueryableRow> BStorePortalsQueryable([Service] Znode_Entities db) =>
        db.ZnodePortals.AsNoTracking()
            .Where(p => p.IsBStore)
            .Select(p => new BStorePortalQueryableRow
            {
                PortalId         = p.PortalId,
                ParentPortalId   = p.ParentPortalId,
                StoreName        = p.StoreName,
                StoreCode        = p.StoreCode,
                IsActive         = p.IsActive,
                IsBStore         = p.IsBStore,
                BStoreDomainName = p.BStoreDomainName
            });

    [GraphQLDescription("Queryable B-store catalog assignments (ZnodeBStoresAvailableCatalogs + PIM catalog).")]
    [UseProjection]
    [UseFiltering]
    [UseSorting]
    public IQueryable<BStoreCatalogAssignmentQueryableRow> BStoreCatalogAssignmentsQueryable([Service] Znode_Entities db) =>
        from bac in db.ZnodeBStoresAvailableCatalogs.AsNoTracking()
        join cat in db.ZnodePimCatalogs.AsNoTracking() on bac.PublishCatalogId equals cat.PimCatalogId
        select new BStoreCatalogAssignmentQueryableRow
        {
            BStoresAvailableCatalogId = bac.BStoresAvailableCatalogId,
            PortalId                  = bac.PortalId,
            PublishCatalogId          = bac.PublishCatalogId,
            CatalogName               = cat.CatalogName,
            CatalogCode               = cat.CatalogCode,
            IsManageInBStore          = bac.IsManageInBStore,
            IsDefaultCatalog          = bac.IsDefaultCatalog
        };

    [GraphQLDescription("Queryable B-store price-list assignments (ZnodeBStoresAvailablePriceLists + price list).")]
    [UseProjection]
    [UseFiltering]
    [UseSorting]
    public IQueryable<BStorePriceListAssignmentQueryableRow> BStorePriceListAssignmentsQueryable([Service] Znode_Entities db) =>
        from bpl in db.ZnodeBStoresAvailablePriceLists.AsNoTracking()
        join pl in db.ZnodePriceLists.AsNoTracking() on bpl.PriceListId equals pl.PriceListId
        select new BStorePriceListAssignmentQueryableRow
        {
            BStoresAvailablePriceListId = bpl.BStoresAvailablePriceListId,
            PortalId                    = bpl.PortalId,
            PriceListId                 = pl.PriceListId,
            ListCode                    = pl.ListCode,
            ListName                    = pl.ListName,
            IsManageInBStore            = bpl.IsManageInBStore,
            IsDefaultPriceList          = bpl.IsDefaultPriceList
        };

    [GraphQLDescription("Queryable domains (ZnodeDomains).")]
    [UseProjection]
    [UseFiltering]
    [UseSorting]
    public IQueryable<DomainQueryableRow> DomainsQueryable([Service] Znode_Entities db) =>
        db.ZnodeDomains.AsNoTracking()
            .Select(d => new DomainQueryableRow
            {
                DomainId         = d.DomainId,
                PortalId         = d.PortalId,
                DomainName       = d.DomainName,
                IsActive         = d.IsActive,
                IsDefault        = d.IsDefault,
                ApplicationType  = d.ApplicationType
            });
}
