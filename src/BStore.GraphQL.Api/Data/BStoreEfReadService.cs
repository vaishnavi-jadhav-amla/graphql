using BStore.GraphQL.Api.Common;
using BStore.GraphQL.Api.GraphQL.Types;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Znode.Libraries.Data.DataModel;
using Znode.Libraries.Data.ZnodeEntity;

namespace BStore.GraphQL.Api.Data;

/// <summary>
/// Reads B-store projection data from <see cref="Znode_Entities"/> (aligned with znode10-api-migration schema).
/// </summary>
public sealed class BStoreEfReadService(
    Znode_Entities db,
    ILogger<BStoreEfReadService> log) : IBStoreEfReadService
{
    public async Task<BStoreListResult?> GetStoresAsync(int parentPortalId, int userId, CancellationToken ct)
    {
        try
        {
            // Project to the projection columns BEFORE materializing so EF only requests these
            // fields (some legacy ZnodePortal columns like SMTPAuthenticationType are not present
            // in every customer schema).
            var rows = await (
                    from up in db.ZnodeUserPortals.AsNoTracking()
                    join p in db.ZnodePortals.AsNoTracking() on up.PortalId equals p.PortalId
                    where up.UserId == userId
                          && p.ParentPortalId == parentPortalId
                          && p.IsBStore
                    orderby p.ModifiedDate descending
                    select new
                    {
                        p.PortalId,
                        p.StoreName,
                        p.BStoreDomainName,
                        p.IsActive,
                        p.StoreCode,
                        p.CreatedDate,
                        p.ModifiedDate,
                        p.LogoPath
                    })
                .ToListAsync(ct);

            var items = rows
                .Select(p => new BStoreListItem
                {
                    PortalId     = p.PortalId,
                    StoreName    = p.StoreName ?? "",
                    DomainUrl    = p.BStoreDomainName,
                    IsActive     = p.IsActive,
                    StoreCode    = p.StoreCode,
                    CreatedDate  = p.CreatedDate.ToString("o"),
                    ModifiedDate = p.ModifiedDate.ToString("o"),
                    MediaId      = null,
                    LogoUrl      = p.LogoPath
                })
                .ToList();

            return new BStoreListResult
            {
                BStores         = items,
                IsBStoreManager = false,
                IsBStoreOwner   = false
            };
        }
        catch (Exception ex)
        {
            log.LogError(ex, "GetStoresAsync EF failed parentPortalId={ParentPortalId} userId={UserId}", parentPortalId, userId);
            throw;
        }
    }

    public async Task<BStoreDetails?> GetStoreDetailsAsync(int storeId, CancellationToken ct)
    {
        // Server-side projection — never materialize the whole ZnodePortal entity, since some
        // optional columns (e.g. SMTPAuthenticationType) may be absent from older DB snapshots.
        var p = await db.ZnodePortals.AsNoTracking()
            .Where(x => x.PortalId == storeId && x.IsBStore)
            .Select(x => new
            {
                x.PortalId,
                x.StoreName,
                x.BStoreDomainName,
                x.IsActive,
                x.StoreCode,
                x.ParentPortalId,
                x.IsBStoreUsersSelfRegister,
                x.MobileTheme,
                x.ExternalID
            })
            .FirstOrDefaultAsync(ct);
        if (p is null) return null;

        var title = await db.ZnodeCMSPortalThemes.AsNoTracking()
            .Where(t => t.PortalId == storeId)
            .Select(t => t.WebsiteTitle)
            .FirstOrDefaultAsync(ct);

        return new BStoreDetails
        {
            PortalId               = p.PortalId,
            StoreName              = p.StoreName ?? "",
            DomainURL              = p.BStoreDomainName,
            IsActive               = p.IsActive,
            StoreCode              = p.StoreCode,
            ParentPortalId         = p.ParentPortalId,
            CustomCSSClass         = null,
            GlobalAttributes       = null,
            LocaleCode             = null,
            CurrencyCode           = null,
            TimeZone               = null,
            WebsiteTitle           = title,
            EnableGuestUserSignup  = p.IsBStoreUsersSelfRegister,
            EnableUserRegistration = p.IsBStoreUsersSelfRegister,
            CSSThemeName           = p.MobileTheme,
            ExternalId             = p.ExternalID
        };
    }

    public async Task<BStoreDesign?> GetStoreDesignAsync(int storeId, CancellationToken ct)
    {
        var theme = await db.ZnodeCMSPortalThemes.AsNoTracking()
            .Include(t => t.CMSTheme)
            .Include(t => t.CMSThemeCSS)
            .FirstOrDefaultAsync(t => t.PortalId == storeId, ct);
        if (theme is null) return null;

        string? logoPath = null;
        string? favPath  = null;
        if (theme.MediaId is > 0)
        {
            logoPath = await db.ZnodeMedia.AsNoTracking()
                .Where(m => m.MediaId == theme.MediaId)
                .Select(m => m.Path)
                .FirstOrDefaultAsync(ct);
        }

        if (theme.FavIconId is > 0)
        {
            favPath = await db.ZnodeMedia.AsNoTracking()
                .Where(m => m.MediaId == theme.FavIconId)
                .Select(m => m.Path)
                .FirstOrDefaultAsync(ct);
        }

        return new BStoreDesign
        {
            WebsiteTitle   = theme.WebsiteTitle,
            MediaId        = theme.MediaId,
            FaviconPath    = favPath,
            FaviconMediaId = theme.FavIconId,
            LogoPath       = logoPath,
            CSSThemeName   = theme.CMSTheme?.Name,
            CustomCSS      = theme.CMSThemeCSS?.CSSName,
            PrimaryColor   = theme.Color1,
            SecondaryColor = theme.Color2
        };
    }

    public async Task<List<CatalogItem>?> GetCatalogsAsync(
        int portalId, bool associated, int pageIndex, int pageSize, string? filter,
        CancellationToken ct)
    {
        pageIndex = Math.Max(pageIndex, 1);
        pageSize  = Math.Max(pageSize, 1);

        var query =
            from bac in db.ZnodeBStoresAvailableCatalogs.AsNoTracking()
            join cat in db.ZnodePimCatalogs.AsNoTracking() on bac.PublishCatalogId equals cat.PimCatalogId
            where bac.PortalId == portalId
            select new { bac, cat };

        if (!string.IsNullOrEmpty(filter) &&
            filter.Contains("ismanageinbstore", StringComparison.OrdinalIgnoreCase))
            query = query.Where(x => x.bac.IsManageInBStore);

        var list = await query
            .OrderBy(x => x.cat.CatalogName)
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new CatalogItem
            {
                PublishCatalogId = x.bac.PublishCatalogId,
                CatalogName        = x.cat.CatalogName ?? "",
                CatalogCode        = x.cat.CatalogCode,
                IsAssociated       = associated
            })
            .ToListAsync(ct);

        return list;
    }

    public async Task<List<PriceListItem>?> GetPriceListsAsync(
        int portalId, bool associated, int pageIndex, int pageSize,
        CancellationToken ct)
    {
        pageIndex = Math.Max(pageIndex, 1);
        pageSize  = Math.Max(pageSize, 1);

        var query =
            from bpl in db.ZnodeBStoresAvailablePriceLists.AsNoTracking()
            join pl in db.ZnodePriceLists.AsNoTracking() on bpl.PriceListId equals pl.PriceListId
            where bpl.PortalId == portalId
            select new { bpl, pl };

        return await query
            .OrderBy(x => x.pl.ListName)
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new PriceListItem
            {
                PriceListId   = x.pl.PriceListId,
                PriceListName = x.pl.ListName ?? x.pl.ListCode,
                PriceListCode = x.pl.ListCode,
                IsAssociated  = associated
            })
            .ToListAsync(ct);
    }

    public async Task<string?> GetDomainNameSuffixAsync(int parentPortalId, CancellationToken ct)
    {
        return await db.ZnodePortals.AsNoTracking()
            .Where(p => p.PortalId == parentPortalId)
            .Select(p => p.BStoreDomainName)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<List<DomainListItem>?> GetDomainListAsync(int pageIndex, int pageSize, CancellationToken ct)
    {
        pageIndex = Math.Max(pageIndex, 1);
        pageSize  = Math.Max(pageSize, 1);

        return await db.ZnodeDomains.AsNoTracking()
            .OrderBy(d => d.DomainName)
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .Select(d => new DomainListItem
            {
                DomainId   = d.DomainId,
                DomainName = d.DomainName,
                IsActive   = d.IsActive
            })
            .ToListAsync(ct);
    }
}
