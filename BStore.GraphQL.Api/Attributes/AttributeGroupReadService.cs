using System.Text.Json;
using BStore.GraphQL.Api.Caching;
using BStore.GraphQL.Api.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Znode.Libraries.Data.ZnodeEntity;
using ICacheService = BStore.GraphQL.Api.Caching.ICacheService;

namespace BStore.GraphQL.Api.Attributes;

/// <summary>
/// Reads <see cref="Znode.Libraries.Data.PublishDataModel.ZnodePublishPortalGlobalAttributeEntity"/>
/// and surfaces the JSON <c>GlobalAttributeGroups</c> blob as strongly-typed groups (ADR-001 + ADR-009).
/// </summary>
public sealed class AttributeGroupReadService(
    IDbContextFactory<ZnodePublish_Entities> dbFactory,
    ICacheService cache,
    CacheTtlProfile ttl,
    IRequestDebugContext debug,
    ILogger<AttributeGroupReadService> logger) : IAttributeGroupReadService
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<IReadOnlyList<GlobalAttributeGroup>> GetGroupsAsync(
        int portalId, string locale, CancellationToken ct)
    {
        var key = CacheKeys.GlobalAttributeGroups(portalId, locale);
        var box = await cache.GetOrSetAsync(key, () => LoadAsync(portalId, locale, ct), ttl.Lookup, ct);
        return box?.Groups ?? Array.Empty<GlobalAttributeGroup>();
    }

    private async Task<GroupBox?> LoadAsync(int portalId, string locale, CancellationToken ct)
    {
        debug.RecordDataSource(DataSource.ZnodePublishEntities);
        using var _ = debug.Stage("attributes.globalGroups.ef");
        await using var db = await dbFactory.CreateDbContextAsync(ct);

        var entity = await db.ZnodePublishPortalGlobalAttributeEntities.AsNoTracking()
            .Where(a => a.PortalId == portalId)
            .OrderByDescending(a => a.VersionId)
            .FirstOrDefaultAsync(ct);

        if (entity is null || string.IsNullOrWhiteSpace(entity.GlobalAttributeGroups))
            return new GroupBox(Array.Empty<GlobalAttributeGroup>());

        try
        {
            var raw = JsonSerializer.Deserialize<List<RawGroup>>(entity.GlobalAttributeGroups, Json) ?? new();
            var groups = raw.Select(g => new GlobalAttributeGroup
            {
                GroupId = g.GroupId,
                Code    = g.Code ?? g.Name ?? $"group-{g.GroupId}",
                Name    = g.Name ?? g.Code ?? $"group-{g.GroupId}",
                Attributes = (g.Attributes ?? new())
                    .Select(a => new GlobalAttribute
                    {
                        AttributeId = a.AttributeId,
                        Code        = a.Code ?? "",
                        Name        = a.Name ?? a.Code ?? "",
                        Value       = a.Value,
                        DataType    = a.DataType,
                        Locale      = a.Locale ?? locale
                    })
                    .ToList()
            }).ToList();

            return new GroupBox(groups);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to parse GlobalAttributeGroups JSON for portal {PortalId}", portalId);
            return new GroupBox(Array.Empty<GlobalAttributeGroup>());
        }
    }

    private sealed record RawGroup(int GroupId, string? Code, string? Name, List<RawAttr>? Attributes);
    private sealed record RawAttr(int AttributeId, string? Code, string? Name, string? Value, string? DataType, string? Locale);
    private sealed record GroupBox(IReadOnlyList<GlobalAttributeGroup> Groups);
}
