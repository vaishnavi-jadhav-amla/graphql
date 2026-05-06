using BStore.GraphQL.Api.Diagnostics;
using BStore.GraphQL.Api.GraphQL.Types;
using Microsoft.EntityFrameworkCore;
using Znode.Libraries.Data.ZnodeEntity;

namespace BStore.GraphQL.Api.Search;

/// <summary>
/// Dev / fallback search using SQL <c>LIKE</c> against published product detail. ADR-011 prohibits this
/// in production — production must register <see cref="AzureCognitiveSearchProvider"/> instead.
/// </summary>
public sealed class SqlLikeSearchProvider(
    IDbContextFactory<Znode_Entities> dbFactory,
    IRequestDebugContext debug) : ISearchProvider
{
    public string ProviderName => "Sql:LIKE";

    public async Task<SearchHits<T>> SearchAsync<T>(SearchQuery query, CancellationToken ct) where T : class
    {
        debug.RecordDataSource(DataSource.ZnodeEntities);
        debug.Note("search.sqlLike", $"index={query.Index} term={query.Text}");

        if (typeof(T) != typeof(ProductRow))
            throw new NotSupportedException($"SqlLikeSearchProvider only supports ProductRow, not {typeof(T).Name}");

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var t = (query.Text ?? "").Trim().ToLowerInvariant();
        var q = db.ZnodePublishProductDetails.AsNoTracking()
            .Where(d => d.PublishProductId != null
                        && (
                            (d.ProductName != null && d.ProductName.ToLower().Contains(t))
                            || (d.SKU != null && d.SKU.ToLower().Contains(t))));

        var total = await q.CountAsync(ct);
        var rows = await q.OrderBy(d => d.ProductName)
            .Skip(query.Skip).Take(query.Top)
            .Select(d => new ProductRow
            {
                Id    = d.PublishProductId!.Value,
                Title = d.ProductName ?? "",
                Sku   = d.SKU
            })
            .ToListAsync(ct);

        return new SearchHits<T>((IReadOnlyList<T>)rows, total, ProviderName);
    }
}
