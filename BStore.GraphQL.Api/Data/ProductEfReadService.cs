using BStore.GraphQL.Api.GraphQL.Types;
using Microsoft.EntityFrameworkCore;
using Znode.Libraries.Data.ZnodeEntity;

namespace BStore.GraphQL.Api.Data;

public sealed class ProductEfReadService(Znode_Entities db) : IProductCatalogReadService
{
    public async Task<ProductListResult?> GetProductsAsync(
        int limit, int skip, string? sortBy, string? order, string? select, CancellationToken ct)
    {
        _ = select;
        var baseQuery = ProductRowsQueryable();
        var sorted    = ApplySort(baseQuery, sortBy, order);
        var total     = await sorted.CountAsync(ct);
        var rows      = await sorted.Skip(skip).Take(limit).ToListAsync(ct);
        return new ProductListResult { Products = rows.Select(ToGraphqlRow).ToList(), Total = total, Skip = skip, Limit = limit };
    }

    public async Task<ProductRow?> GetProductByIdAsync(int id, CancellationToken ct)
    {
        var row = await ProductRowsQueryable()
            .Where(p => p.Id == id)
            .FirstOrDefaultAsync(ct);
        return row is null ? null : ToGraphqlRow(row);
    }

    public async Task<ProductListResult?> SearchProductsAsync(string q, int limit, int skip, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(q))
            return new ProductListResult { Products = [], Total = 0, Skip = skip, Limit = limit };

        var t = q.Trim().ToLowerInvariant();
        var filtered = ProductRowsQueryable()
            .Where(p =>
                (p.Title != null && p.Title.ToLower().Contains(t))
                || (p.Sku != null && p.Sku.ToLower().Contains(t)));

        var total = await filtered.CountAsync(ct);
        var rows  = await filtered.OrderBy(p => p.Title).Skip(skip).Take(limit).ToListAsync(ct);
        return new ProductListResult
        {
            Products = rows.Select(ToGraphqlRow).ToList(),
            Total    = total,
            Skip     = skip,
            Limit    = limit
        };
    }

    public async Task<List<ProductCategoryRow>?> GetCategoriesAsync(CancellationToken ct)
    {
        return await db.ZnodePublishCategoryDetails.AsNoTracking()
            .Where(cd => cd.CategoryCode != null && cd.IsActive != false)
            .GroupBy(cd => cd.CategoryCode!)
            .Select(g => new ProductCategoryRow
            {
                Slug = g.Key,
                Name = g.Max(x => x.PublishCategoryName ?? x.CategoryCode) ?? g.Key,
                Url  = ""
            })
            .OrderBy(x => x.Name)
            .Take(500)
            .ToListAsync(ct);
    }

    public async Task<ProductListResult?> GetProductsByCategoryAsync(
        string category, int limit, int skip, CancellationToken ct)
    {
        var publishCategoryIds = await db.ZnodePublishCategoryDetails.AsNoTracking()
            .Where(cd => cd.CategoryCode == category)
            .Select(cd => cd.PublishCategoryId)
            .Where(id => id != null)
            .Distinct()
            .ToListAsync(ct);

        if (publishCategoryIds.Count == 0)
            return new ProductListResult { Products = [], Total = 0, Skip = skip, Limit = limit };

        var productIds = db.ZnodePublishCategoryProducts.AsNoTracking()
            .Where(cp => cp.PublishCategoryId != null
                         && publishCategoryIds.Contains(cp.PublishCategoryId!.Value))
            .Select(cp => cp.PublishProductId)
            .Distinct();

        var filtered = ProductRowsQueryable().Where(p => productIds.Contains(p.Id));
        var total      = await filtered.CountAsync(ct);
        var rows       = await filtered.OrderBy(p => p.Title).Skip(skip).Take(limit).ToListAsync(ct);
        return new ProductListResult
        {
            Products = rows.Select(ToGraphqlRow).ToList(),
            Total    = total,
            Skip     = skip,
            Limit    = limit
        };
    }

    private sealed class ProductRowCore
    {
        public int Id { get; init; }
        public string Title { get; init; } = "";
        public string? Sku { get; init; }
    }

    private IQueryable<ProductRowCore> ProductRowsQueryable()
    {
        var primaryKeys =
            from d in db.ZnodePublishProductDetails.AsNoTracking()
            where d.PublishProductId != null
            group d by d.PublishProductId!.Value
            into g
            select new
            {
                PublishProductId = g.Key,
                MinInfoId        = g.Min(x => x.PublishProductInfoId)
            };

        return
            from pk in primaryKeys
            join d in db.ZnodePublishProductDetails.AsNoTracking() on pk.MinInfoId equals d.PublishProductInfoId
            select new ProductRowCore
            {
                Id    = pk.PublishProductId,
                Title = d.ProductName ?? "",
                Sku   = d.SKU
            };
    }

    private static ProductRow ToGraphqlRow(ProductRowCore p) =>
        new()
        {
            Id                 = p.Id,
            Title              = p.Title,
            Description        = "",
            Category           = "",
            Price              = 0m,
            DiscountPercentage = 0,
            Rating             = 0,
            Stock              = 0,
            Brand              = null,
            Sku                = p.Sku,
            Thumbnail          = null,
            Tags               = [],
            Images             = []
        };

    private static IQueryable<ProductRowCore> ApplySort(IQueryable<ProductRowCore> q, string? sortBy, string? order)
    {
        var desc = string.Equals(order, "desc", StringComparison.OrdinalIgnoreCase);
        return (sortBy?.ToLowerInvariant()) switch
        {
            "title" => desc ? q.OrderByDescending(p => p.Title) : q.OrderBy(p => p.Title),
            "price" => desc ? q.OrderByDescending(p => p.Id) : q.OrderBy(p => p.Id),
            _       => desc ? q.OrderByDescending(p => p.Id) : q.OrderBy(p => p.Id)
        };
    }
}
