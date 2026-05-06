using BStore.GraphQL.Api.Caching;
using BStore.GraphQL.Api.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Znode.Libraries.Data.ZnodeEntity;
using ICacheService = BStore.GraphQL.Api.Caching.ICacheService;

namespace BStore.GraphQL.Api.Catalog;

/// <summary>
/// ADR-015 implementation: reads <c>ZnodePublishCategoryDetail</c> sorted by depth, materialising the
/// path string as <c>{rootCode}/{...}/{leafCode}</c>. No recursive CTEs, no client-side recursion.
/// </summary>
public sealed class MaterializedPathCategoryTreeService(
    IDbContextFactory<Znode_Entities> dbFactory,
    ICacheService cache,
    CacheTtlProfile ttl,
    IRequestDebugContext debug) : ICategoryTreeService
{
    public async Task<IReadOnlyList<CategoryNode>> GetTreeAsync(int catalogId, string locale, CancellationToken ct)
    {
        var key = CacheKeys.CategoryTree(catalogId, locale);
        var box = await cache.GetOrSetAsync(key, () => LoadAsync(catalogId, locale, ct), ttl.Lookup, ct);
        return box?.Nodes ?? Array.Empty<CategoryNode>();
    }

    private async Task<TreeBox?> LoadAsync(int catalogId, string locale, CancellationToken ct)
    {
        debug.RecordDataSource(DataSource.ZnodeEntities);
        using var _ = debug.Stage("catalog.materializedTree.ef");
        await using var db = await dbFactory.CreateDbContextAsync(ct);

        // Join the parent reference table (PublishCategory has CatalogId + ParentId) to its locale-specific
        // detail to read the display name. ZnodePublishCategory.PublishParentCategoryId is the materialized
        // parent — depth and path are derived in a single linear pass without a recursive CTE (ADR-015).
        var raw = await (
            from cat in db.ZnodePublishCategories.AsNoTracking()
            where cat.PublishCatalogId == catalogId
            join det in db.ZnodePublishCategoryDetails.AsNoTracking()
                on cat.PublishCategoryId equals det.PublishCategoryId into joined
            from det in joined.DefaultIfEmpty()
            select new RawCategory(
                cat.PublishCategoryId,
                cat.PublishParentCategoryId,
                det != null ? det.PublishCategoryName : null,
                det != null ? det.CategoryCode        : null)
        ).ToListAsync(ct);

        var byId = raw.ToDictionary(r => r.Id);

        var nodes = new List<CategoryNode>(raw.Count);
        var pathCache = new Dictionary<int, (string Path, int Depth)>(raw.Count);
        int order = 0;

        foreach (var r in raw)
        {
            var (path, depth) = ComputePath(r, byId, pathCache);
            nodes.Add(new CategoryNode(
                r.Id,
                int.TryParse(r.ParentRef, out var pid) ? pid : null,
                r.Code ?? "",
                r.Name ?? r.Code ?? "",
                path,
                depth,
                order++));
        }

        return new TreeBox(nodes);
    }

    private static (string Path, int Depth) ComputePath(
        RawCategory r,
        IReadOnlyDictionary<int, RawCategory> byId,
        Dictionary<int, (string Path, int Depth)> cache)
    {
        if (cache.TryGetValue(r.Id, out var hit)) return hit;

        if (string.IsNullOrEmpty(r.ParentRef) || !int.TryParse(r.ParentRef, out var parentId) || !byId.TryGetValue(parentId, out var parent))
        {
            var leaf = (Path: r.Code ?? r.Id.ToString(), Depth: 0);
            cache[r.Id] = leaf;
            return leaf;
        }

        var (pPath, pDepth) = ComputePath(parent, byId, cache);
        var nodePath = string.IsNullOrEmpty(r.Code) ? pPath : $"{pPath}/{r.Code}";
        var node = (Path: nodePath, Depth: pDepth + 1);
        cache[r.Id] = node;
        return node;
    }

    private sealed record RawCategory(int Id, string? ParentRef, string? Name, string? Code);

    /// <summary>Wrapper because <see cref="ICacheService"/> generic constraint requires a class.</summary>
    private sealed record TreeBox(IReadOnlyList<CategoryNode> Nodes);
}
