using BStore.GraphQL.Api.Configuration;
using BStore.GraphQL.Api.Diagnostics;
using BStore.GraphQL.Api.GraphQL.Queries;
using BStore.GraphQL.Api.Media;
using HotChocolate;
using HotChocolate.Types;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Znode.Libraries.Data.ZnodeEntity;
using ICacheService = BStore.GraphQL.Api.Caching.ICacheService;

namespace BStore.GraphQL.Api.Storefront;

/// <summary>
/// ADR-008: <c>websiteEntry(portalCode, locale, path)</c> single entry-point query for storefront pages.
/// ADR-009: reads exclusively from <see cref="ZnodePublish_Entities"/> — no EAV joins.
/// ADR-014: media URLs are CDN-prefixed via <see cref="MediaUrlBuilder"/>.
/// </summary>
[ExtendObjectType(typeof(BStoreQuery))]
public sealed class WebsiteEntryResolvers(
    ICacheService cache,
    MediaUrlBuilder media,
    IOptions<GraphQLOptions> options)
{
    private readonly GraphQLOptions _opts = options.Value;

    [GraphQLDescription("ADR-008: single entry-point query for storefront pages (theme + SEO + canonical url).")]
    public async Task<WebsiteEntry?> WebsiteEntry(
        string portalCode,
        string locale,
        string path,
        [Service] IDbContextFactory<ZnodePublish_Entities> publishFactory,
        [Service] IRequestDebugContext debug,
        CancellationToken ct)
    {
        debug.RecordDataSource(DataSource.ZnodePublishEntities);
        debug.Note("storefront.websiteEntry", $"portal={portalCode} locale={locale} path={path}");

        var key = $"website:entry:{portalCode}:{locale}:{path}";
        return await cache.GetOrSetAsync(key, async () =>
        {
            using (debug.Stage("storefront.websiteEntry.ef"))
            {
                await using var db = await publishFactory.CreateDbContextAsync(ct);

                var web = await db.ZnodePublishWebstoreEntities.AsNoTracking()
                    .FirstOrDefaultAsync(w => w.PortalId > 0, ct);

                if (web is null) return null;

                var seo = await db.ZnodePublishSeoEntities.AsNoTracking()
                    .FirstOrDefaultAsync(s => s.PortalId == web.PortalId, ct);

                return new WebsiteEntry
                {
                    PortalId   = web.PortalId,
                    PortalCode = portalCode,
                    Locale     = locale,
                    Theme = new WebsiteEntryTheme
                    {
                        ThemeName      = web.ThemeName,
                        LogoUrl        = media.ToCdnUrl(web.WebsiteLogo),
                        FaviconUrl     = media.ToCdnUrl(web.FaviconImage),
                        PrimaryColor   = web.Color1,
                        SecondaryColor = web.Color2
                    },
                    Seo = seo is null ? null : new WebsiteEntrySeo
                    {
                        Title       = seo.SEOTitle,
                        Description = seo.SEODescription,
                        Keywords    = seo.SEOKeywords,
                        Url         = path
                    },
                    CanonicalUrl = path,
                    PageType     = "page"
                };
            }
        }, TimeSpan.FromSeconds(_opts.LookupCacheExpirySeconds), ct);
    }
}
