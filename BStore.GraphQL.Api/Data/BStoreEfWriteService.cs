using BStore.GraphQL.Api.Configuration;
using BStore.GraphQL.Api.GraphQL.Types;
using HotChocolate.Types;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Znode.Libraries.Data.DataModel;
using Znode.Libraries.Data.ZnodeEntity;
using Path = System.IO.Path;

namespace BStore.GraphQL.Api.Data;

/// <summary>
/// EF / raw-SQL B-store writes against <c>Znode_Entities</c>. All <c>ZnodePortal</c> writes use raw SQL
/// because the EF model includes legacy columns (e.g. <c>SMTPAuthenticationType</c>) that may be absent
/// from a customer schema; raw SQL only references columns we actually need.
/// </summary>
public sealed class BStoreEfWriteService(
    Znode_Entities db,
    IOptions<MediaOptions> mediaOptions,
    ILogger<BStoreEfWriteService> log) : IBStoreEfWriteService
{
    private readonly MediaOptions _media = mediaOptions.Value;

    // ── Activation ────────────────────────────────────────────────────────────

    public async Task<bool> SetActivationAsync(int storeId, int userId, bool active, CancellationToken ct)
    {
        var rows = await db.Database.ExecuteSqlRawAsync(
            "UPDATE ZnodePortals SET IsActive=@active, ModifiedBy=@uid, ModifiedDate=SYSUTCDATETIME() " +
            "WHERE PortalId=@pid AND IsBStore=1",
            new SqlParameter("@active", active),
            new SqlParameter("@uid",    userId > 0 ? userId : 1),
            new SqlParameter("@pid",    storeId)
            );
        return rows > 0;
    }

    // ── Update settings ───────────────────────────────────────────────────────

    public async Task<bool> UpdateStoreAsync(
        int storeId, int userId, UpdateBStoreSettingsInput input, CancellationToken ct)
    {
        var sets = new List<string>();
        var pars = new List<SqlParameter>
        {
            new("@pid", storeId),
            new("@uid", userId > 0 ? userId : 1)
        };

        if (!string.IsNullOrWhiteSpace(input.StoreName)) { sets.Add("StoreName=@storeName");  pars.Add(new("@storeName", input.StoreName)); }
        if (!string.IsNullOrWhiteSpace(input.DomainURL)) { sets.Add("BStoreDomainName=@dom"); pars.Add(new("@dom",       input.DomainURL)); }
        if (input.IsActive.HasValue)                    { sets.Add("IsActive=@active");      pars.Add(new("@active",    input.IsActive.Value)); }
        if (!string.IsNullOrWhiteSpace(input.ExternalId)) { sets.Add("ExternalID=@ext");     pars.Add(new("@ext",       input.ExternalId)); }
        if (input.EnableGuestUserSignup.HasValue || input.EnableUserRegistration.HasValue)
        {
            sets.Add("IsBStoreUsersSelfRegister=@self");
            pars.Add(new("@self", input.EnableGuestUserSignup ?? input.EnableUserRegistration ?? false));
        }

        if (sets.Count == 0)
            return false;

        sets.Add("ModifiedBy=@uid");
        sets.Add("ModifiedDate=SYSUTCDATETIME()");

        var sql = $"UPDATE ZnodePortals SET {string.Join(", ", sets)} WHERE PortalId=@pid AND IsBStore=1";
        var rows = await db.Database.ExecuteSqlRawAsync(sql, pars.ToArray());
        if (rows == 0) return false;

        if (!string.IsNullOrWhiteSpace(input.DomainURL))
            await SyncDefaultDomainNameAsync(storeId, input.DomainURL!, userId, ct);

        return true;
    }

    // ── Update theme ─────────────────────────────────────────────────────────

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

        if (input.WebsiteTitle is not null)                 theme.WebsiteTitle = input.WebsiteTitle;
        if (input.MediaId.HasValue)                         theme.MediaId      = input.MediaId;
        if (input.FaviconMediaId.HasValue)                  theme.FavIconId    = input.FaviconMediaId;
        if (!string.IsNullOrWhiteSpace(input.PrimaryColor)) theme.Color1       = input.PrimaryColor;
        if (!string.IsNullOrWhiteSpace(input.SecondaryColor)) theme.Color2     = input.SecondaryColor;

        if (!string.IsNullOrWhiteSpace(input.CSSThemeName))
        {
            var cmsTheme = await db.ZnodeCMSThemes.AsNoTracking()
                .Where(t => t.Name == input.CSSThemeName)
                .Select(t => new { t.CMSThemeId })
                .FirstOrDefaultAsync(ct);
            if (cmsTheme is null)
            {
                log.LogWarning("UpdateStoreDesignAsync: CMS theme Name={Name} not found", input.CSSThemeName);
                return false;
            }

            theme.CMSThemeId = cmsTheme.CMSThemeId;
            await db.Database.ExecuteSqlRawAsync(
                "UPDATE ZnodePortals SET MobileTheme=@m, ModifiedBy=@uid, ModifiedDate=SYSUTCDATETIME() WHERE PortalId=@pid",
                new SqlParameter("@m",   input.CSSThemeName),
                new SqlParameter("@uid", userId > 0 ? userId : 1),
                new SqlParameter("@pid", storeId));
        }

        if (!string.IsNullOrWhiteSpace(input.CustomCSS))
        {
            var themeId = theme.CMSThemeId;
            var cssRow = await db.ZnodeCMSThemeCSSes.AsNoTracking()
                .Where(c => c.CMSThemeId == themeId && c.CSSName == input.CustomCSS)
                .Select(c => new { c.CMSThemeCSSId })
                .FirstOrDefaultAsync(ct);
            if (cssRow is null)
            {
                log.LogWarning("UpdateStoreDesignAsync: CSSName={Css} not found", input.CustomCSS);
                return false;
            }
            theme.CMSThemeCSSId = cssRow.CMSThemeCSSId;
        }

        theme.ModifiedBy   = userId > 0 ? userId : theme.ModifiedBy;
        theme.ModifiedDate = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return true;
    }

    // ── Create ───────────────────────────────────────────────────────────────

    public async Task<CreateStoreResult?> CreateStoreAsync(
        int parentPortalId, int userId, CreateBStoreInput input, CancellationToken ct)
    {
        var parent = await db.ZnodePortals.AsNoTracking()
            .Where(p => p.PortalId == parentPortalId)
            .Select(p => new { p.PortalId, p.BStoreDomainName, p.CompanyName, p.ImageNotAvailablePath, p.UserVerificationType })
            .FirstOrDefaultAsync(ct);

        if (parent is null)
            return new CreateStoreResult { HasError = true, ErrorMessage = $"Parent portal {parentPortalId} not found." };

        var storeCode = string.IsNullOrWhiteSpace(input.StoreCode)
            ? $"BS{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}"
            : input.StoreCode!.Trim();

        var codeExists = await db.ZnodePortals.AsNoTracking().AnyAsync(p => p.StoreCode == storeCode, ct);
        if (codeExists)
            return new CreateStoreResult { HasError = true, ErrorMessage = $"Store code '{storeCode}' is already in use." };

        var domain = string.IsNullOrWhiteSpace(input.DomainURL) ? parent.BStoreDomainName : input.DomainURL;

        // Insert the portal via raw SQL — this avoids EF emitting the legacy SMTPAuthenticationType column.
        var newPortalIdParam = new SqlParameter
        {
            ParameterName = "@newId",
            SqlDbType     = System.Data.SqlDbType.Int,
            Direction     = System.Data.ParameterDirection.Output
        };

        await db.Database.ExecuteSqlRawAsync(@"
            INSERT INTO ZnodePortals
                (CompanyName, StoreName, ImageNotAvailablePath, UserVerificationType, ShowSwatchInCategory,
                 ShowAlternateImageInCategory, UseSSL, IsActive, IsApproved, IsBStore, IsBStoreRequestPage,
                 IsBStoreUsersSelfRegister, IsBStoreLoginRequired, IsBStoreShowPricing, IsCatalogCustomized,
                 IsCentralizedLoginRestricted, ParentPortalId, StoreCode, BStoreDomainName,
                 CreatedBy, CreatedDate, ModifiedBy, ModifiedDate)
            VALUES
                (@company, @name, @imgNotAvail, @uvt, 0,
                 0, 0, @active, 1, 1, 0,
                 1, 1, 1, 0,
                 0, @parent, @code, @dom,
                 @uid, SYSUTCDATETIME(), @uid, SYSUTCDATETIME());
            SELECT @newId = CAST(SCOPE_IDENTITY() AS INT);",
            new SqlParameter("@company",     parent.CompanyName),
            new SqlParameter("@name",        input.StoreName),
            new SqlParameter("@imgNotAvail", parent.ImageNotAvailablePath),
            new SqlParameter("@uvt",         parent.UserVerificationType),
            new SqlParameter("@active",      input.IsActive),
            new SqlParameter("@parent",      parentPortalId),
            new SqlParameter("@code",        storeCode),
            new SqlParameter("@dom",         (object?)domain ?? DBNull.Value),
            new SqlParameter("@uid",         userId > 0 ? userId : 1),
            newPortalIdParam);

        var newPortalId = (int)newPortalIdParam.Value;

        if (!string.IsNullOrWhiteSpace(domain))
        {
            db.ZnodeDomains.Add(new ZnodeDomain
            {
                PortalId     = newPortalId,
                DomainName   = domain!,
                IsActive     = true,
                IsDefault    = true,
                ApplicationType = "WebStore",
                CreatedBy    = userId > 0 ? userId : 1,
                CreatedDate  = DateTime.UtcNow,
                ModifiedBy   = userId > 0 ? userId : 1,
                ModifiedDate = DateTime.UtcNow
            });
            await db.SaveChangesAsync(ct);
        }

        return new CreateStoreResult
        {
            HasError  = false,
            PortalId  = newPortalId,
            StoreName = input.StoreName,
            DomainURL = domain
        };
    }

    // ── Duplicate ────────────────────────────────────────────────────────────

    public async Task<bool> DuplicateStoreAsync(
        int sourcePortalId, int userId, DuplicateBStoreInput input, CancellationToken ct)
    {
        var src = await db.ZnodePortals.AsNoTracking()
            .Where(p => p.PortalId == sourcePortalId && p.IsBStore)
            .Select(p => new
            {
                p.CompanyName, p.ImageNotAvailablePath, p.UserVerificationType, p.ParentPortalId,
                p.IsActive, p.IsBStoreUsersSelfRegister, p.IsBStoreLoginRequired, p.IsBStoreShowPricing
            })
            .FirstOrDefaultAsync(ct);
        if (src is null) return false;

        var domain    = !string.IsNullOrWhiteSpace(input.BStoreDomainName) ? input.BStoreDomainName : input.DomainURL;
        var storeCode = string.IsNullOrWhiteSpace(input.StoreCode) ? $"BS{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}" : input.StoreCode;

        var newId = new SqlParameter
        {
            ParameterName = "@newId",
            SqlDbType     = System.Data.SqlDbType.Int,
            Direction     = System.Data.ParameterDirection.Output
        };

        await db.Database.ExecuteSqlRawAsync(@"
            INSERT INTO ZnodePortals
                (CompanyName, StoreName, ImageNotAvailablePath, UserVerificationType, ShowSwatchInCategory,
                 ShowAlternateImageInCategory, UseSSL, IsActive, IsApproved, IsBStore, IsBStoreRequestPage,
                 IsBStoreUsersSelfRegister, IsBStoreLoginRequired, IsBStoreShowPricing, IsCatalogCustomized,
                 IsCentralizedLoginRestricted, ParentPortalId, StoreCode, BStoreDomainName,
                 CreatedBy, CreatedDate, ModifiedBy, ModifiedDate)
            VALUES
                (@company, @name, @imgNotAvail, @uvt, 0,
                 0, 0, @active, 1, 1, 0,
                 @selfReg, @loginReq, @showPricing, 0,
                 0, @parent, @code, @dom,
                 @uid, SYSUTCDATETIME(), @uid, SYSUTCDATETIME());
            SELECT @newId = CAST(SCOPE_IDENTITY() AS INT);",
            new SqlParameter("@company",     src.CompanyName),
            new SqlParameter("@name",        input.StoreName),
            new SqlParameter("@imgNotAvail", src.ImageNotAvailablePath),
            new SqlParameter("@uvt",         src.UserVerificationType),
            new SqlParameter("@active",      src.IsActive),
            new SqlParameter("@selfReg",     src.IsBStoreUsersSelfRegister),
            new SqlParameter("@loginReq",    src.IsBStoreLoginRequired),
            new SqlParameter("@showPricing", src.IsBStoreShowPricing),
            new SqlParameter("@parent",      (object?)src.ParentPortalId ?? DBNull.Value),
            new SqlParameter("@code",        storeCode),
            new SqlParameter("@dom",         (object?)domain ?? DBNull.Value),
            new SqlParameter("@uid",         userId > 0 ? userId : 1),
            newId);

        var newPortalId = (int)newId.Value;

        // Clone catalog assignments
        await db.Database.ExecuteSqlRawAsync(@"
            INSERT INTO ZnodeBStoresAvailableCatalog (PortalId, PublishCatalogId, IsManageInBStore, IsDefaultCatalog,
                                                     CreatedBy, CreatedDate, ModifiedBy, ModifiedDate)
            SELECT @newId, PublishCatalogId, IsManageInBStore, IsDefaultCatalog,
                   @uid, SYSUTCDATETIME(), @uid, SYSUTCDATETIME()
            FROM ZnodeBStoresAvailableCatalog WHERE PortalId=@src",
            new SqlParameter("@newId", newPortalId),
            new SqlParameter("@uid",   userId > 0 ? userId : 1),
            new SqlParameter("@src",   sourcePortalId));

        // Clone price-list assignments
        await db.Database.ExecuteSqlRawAsync(@"
            INSERT INTO ZnodeBStoresAvailablePriceList (PortalId, PriceListId, IsManageInBStore, IsDefaultPriceList,
                                                       CreatedBy, CreatedDate, ModifiedBy, ModifiedDate)
            SELECT @newId, PriceListId, IsManageInBStore, IsDefaultPriceList,
                   @uid, SYSUTCDATETIME(), @uid, SYSUTCDATETIME()
            FROM ZnodeBStoresAvailablePriceList WHERE PortalId=@src",
            new SqlParameter("@newId", newPortalId),
            new SqlParameter("@uid",   userId > 0 ? userId : 1),
            new SqlParameter("@src",   sourcePortalId));

        if (!string.IsNullOrWhiteSpace(domain))
        {
            db.ZnodeDomains.Add(new ZnodeDomain
            {
                PortalId        = newPortalId,
                DomainName      = domain!,
                IsActive        = true,
                IsDefault       = true,
                ApplicationType = "WebStore",
                CreatedBy       = userId > 0 ? userId : 1,
                CreatedDate     = DateTime.UtcNow,
                ModifiedBy      = userId > 0 ? userId : 1,
                ModifiedDate    = DateTime.UtcNow
            });
            await db.SaveChangesAsync(ct);
        }

        return true;
    }

    // ── File upload ──────────────────────────────────────────────────────────

    public async Task<FileUploadResult?> UploadFileAsync(IFile file, int mediaId, string? fileType, CancellationToken ct)
    {
        if (file is null)
            return new FileUploadResult { HasError = true, ErrorMessage = "No file supplied." };

        if (file.Length is long len && len > _media.MaxUploadBytes)
            return new FileUploadResult { HasError = true, ErrorMessage = $"File exceeds the {_media.MaxUploadBytes}-byte limit." };

        var folder = Path.GetFullPath(_media.LocalUploadFolder);
        Directory.CreateDirectory(folder);

        var safeName = $"{Guid.NewGuid():N}_{Path.GetFileName(file.Name)}";
        var fullPath = Path.Combine(folder, safeName);

        await using (var input  = file.OpenReadStream())
        await using (var output = File.Create(fullPath))
            await input.CopyToAsync(output, ct);

        var fileSize  = new FileInfo(fullPath).Length;
        var publicUrl = $"{_media.LocalUploadUrlPrefix.TrimEnd('/')}/{safeName}";
        var ext       = Path.GetExtension(file.Name).TrimStart('.');

        var newMediaId = mediaId > 0 ? mediaId : await InsertMediaRowAsync(safeName, publicUrl, fileSize, fileType ?? ext, ct);

        return new FileUploadResult
        {
            HasError = false,
            MediaId  = newMediaId,
            FileName = safeName,
            FilePath = publicUrl
        };
    }

    private async Task<int> InsertMediaRowAsync(string fileName, string publicPath, long size, string type, CancellationToken ct)
    {
        var newId = new SqlParameter
        {
            ParameterName = "@newId",
            SqlDbType     = System.Data.SqlDbType.Int,
            Direction     = System.Data.ParameterDirection.Output
        };

        await db.Database.ExecuteSqlRawAsync(@"
            INSERT INTO ZnodeMedia (FileName, [Path], [Size], [Type], CreatedBy, CreatedDate, ModifiedBy, ModifiedDate, [Version], IsFromSync, IsImageGenerate)
            VALUES (@fn, @p, @sz, @t, 1, SYSUTCDATETIME(), 1, SYSUTCDATETIME(), 1, 0, 0);
            SELECT @newId = CAST(SCOPE_IDENTITY() AS INT);",
            new SqlParameter("@fn", fileName),
            new SqlParameter("@p",  publicPath),
            new SqlParameter("@sz", size.ToString()),
            new SqlParameter("@t",  type),
            newId);

        return (int)newId.Value;
    }

    // ── File remove ──────────────────────────────────────────────────────────

    public async Task<bool> DeleteFileAsync(string mediaIds, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(mediaIds)) return false;

        var ids = mediaIds.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(s => int.TryParse(s, out var i) ? i : 0)
            .Where(i => i > 0)
            .ToArray();
        if (ids.Length == 0) return false;

        var idList = string.Join(",", ids);
        var connection = db.Database.GetDbConnection();
        var fileNames = new List<string>();

        await db.Database.OpenConnectionAsync(ct);
        try
        {
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = $"SELECT FileName FROM ZnodeMedia WHERE MediaId IN ({idList})";
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                if (!reader.IsDBNull(0))
                    fileNames.Add(reader.GetString(0));
            }
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
        }

        foreach (var name in fileNames)
        {
            try
            {
                var disk = Path.Combine(Path.GetFullPath(_media.LocalUploadFolder), name);
                if (File.Exists(disk)) File.Delete(disk);
            }
            catch (Exception ex)
            {
                log.LogDebug(ex, "DeleteFileAsync: could not delete file '{File}'", name);
            }
        }

        var rows = await db.Database.ExecuteSqlRawAsync(
            $"DELETE FROM ZnodeMedia WHERE MediaId IN ({idList})");
        return rows > 0;
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private async Task SyncDefaultDomainNameAsync(int portalId, string domainName, int userId, CancellationToken ct)
    {
        await db.Database.ExecuteSqlRawAsync(
            "UPDATE ZnodeDomains SET DomainName=@dom, ModifiedBy=@uid, ModifiedDate=SYSUTCDATETIME() WHERE PortalId=@pid AND IsDefault=1",
            new SqlParameter("@dom", domainName),
            new SqlParameter("@uid", userId > 0 ? userId : 1),
            new SqlParameter("@pid", portalId));
    }
}
