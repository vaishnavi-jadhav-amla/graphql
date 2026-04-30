using BStore.GraphQL.Api.GraphQL.Types;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Znode.Libraries.Data.DataModel;
using Znode.Libraries.Data.ZnodeEntity;

namespace BStore.GraphQL.Api.Data;

public sealed class BStoreEfWriteService(
    Znode_Entities db,
    ILogger<BStoreEfWriteService> log) : IBStoreEfWriteService
{
    public async Task<bool> SetActivationAsync(int storeId, int userId, bool active, CancellationToken ct)
    {
        var portal = await db.ZnodePortals
            .FirstOrDefaultAsync(x => x.PortalId == storeId && x.IsBStore, ct);
        if (portal is null)
            return false;

        portal.IsActive     = active;
        portal.ModifiedBy   = userId > 0 ? userId : portal.ModifiedBy;
        portal.ModifiedDate = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> UpdateStoreAsync(
        int storeId, int userId, UpdateBStoreSettingsInput input, CancellationToken ct)
    {
        var portal = await db.ZnodePortals
            .FirstOrDefaultAsync(x => x.PortalId == storeId && x.IsBStore, ct);
        if (portal is null)
            return false;

        if (!string.IsNullOrWhiteSpace(input.StoreName))
            portal.StoreName = input.StoreName;

        if (!string.IsNullOrWhiteSpace(input.DomainURL))
        {
            portal.BStoreDomainName = input.DomainURL;
            await SyncDefaultDomainNameAsync(storeId, input.DomainURL, userId, ct);
        }

        if (input.IsActive.HasValue)
            portal.IsActive = input.IsActive.Value;

        if (!string.IsNullOrWhiteSpace(input.ExternalId))
            portal.ExternalID = input.ExternalId;

        if (input.EnableGuestUserSignup.HasValue || input.EnableUserRegistration.HasValue)
        {
            portal.IsBStoreUsersSelfRegister =
                input.EnableGuestUserSignup ?? input.EnableUserRegistration ?? portal.IsBStoreUsersSelfRegister;
        }

        portal.ModifiedBy   = userId > 0 ? userId : portal.ModifiedBy;
        portal.ModifiedDate = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> UpdateStoreDesignAsync(
        int storeId, int userId, UpdateBStoreDesignInput input, CancellationToken ct)
    {
        var theme = await db.ZnodeCMSPortalThemes
            .Include(t => t.CMSTheme)
            .FirstOrDefaultAsync(t => t.PortalId == storeId, ct);
        if (theme is null)
        {
            log.LogWarning("UpdateStoreDesignAsync: no CMS portal theme for PortalId={PortalId}", storeId);
            return false;
        }

        if (input.WebsiteTitle is not null)
            theme.WebsiteTitle = input.WebsiteTitle;

        if (input.MediaId.HasValue)
            theme.MediaId = input.MediaId;

        if (input.FaviconMediaId.HasValue)
            theme.FavIconId = input.FaviconMediaId;

        if (!string.IsNullOrWhiteSpace(input.PrimaryColor))
            theme.Color1 = input.PrimaryColor;

        if (!string.IsNullOrWhiteSpace(input.SecondaryColor))
            theme.Color2 = input.SecondaryColor;

        if (!string.IsNullOrWhiteSpace(input.CSSThemeName))
        {
            var cmsTheme = await db.ZnodeCMSThemes.AsNoTracking()
                .FirstOrDefaultAsync(t => t.Name == input.CSSThemeName, ct);
            if (cmsTheme is null)
            {
                log.LogWarning("UpdateStoreDesignAsync: CMS theme Name={Name} not found", input.CSSThemeName);
                return false;
            }

            theme.CMSThemeId = cmsTheme.CMSThemeId;
            var portalRow = await db.ZnodePortals.FirstOrDefaultAsync(p => p.PortalId == storeId, ct);
            if (portalRow is not null)
                portalRow.MobileTheme = input.CSSThemeName;
        }

        if (!string.IsNullOrWhiteSpace(input.CustomCSS))
        {
            var themeId = theme.CMSThemeId;
            var cssRow = await db.ZnodeCMSThemeCSSes.AsNoTracking()
                .FirstOrDefaultAsync(
                    c => c.CMSThemeId == themeId && c.CSSName == input.CustomCSS,
                    ct);
            if (cssRow is null)
            {
                log.LogWarning(
                    "UpdateStoreDesignAsync: CSSName={Css} not found for CMSThemeId={ThemeId}",
                    input.CustomCSS,
                    themeId);
                return false;
            }

            theme.CMSThemeCSSId = cssRow.CMSThemeCSSId;
        }

        theme.ModifiedBy   = userId > 0 ? userId : theme.ModifiedBy;
        theme.ModifiedDate = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return true;
    }

    private async Task SyncDefaultDomainNameAsync(
        int portalId, string domainName, int userId, CancellationToken ct)
    {
        var defaults = await db.ZnodeDomains
            .Where(d => d.PortalId == portalId && d.IsDefault)
            .ToListAsync(ct);
        foreach (var d in defaults)
        {
            d.DomainName    = domainName;
            d.ModifiedBy   = userId > 0 ? userId : d.ModifiedBy;
            d.ModifiedDate = DateTime.UtcNow;
        }
    }
}
