using BStore.GraphQL.Api.GraphQL.Types;

namespace BStore.GraphQL.Api.Data;

/// <summary>Published PIM catalogue reads from <c>Znode_Entities</c> (no HTTP).</summary>
public interface IProductCatalogReadService
{
    Task<ProductListResult?> GetProductsAsync(
        int limit, int skip, string? sortBy, string? order, string? select, CancellationToken ct);

    Task<ProductRow?> GetProductByIdAsync(int id, CancellationToken ct);

    Task<ProductListResult?> SearchProductsAsync(string q, int limit, int skip, CancellationToken ct);

    Task<List<ProductCategoryRow>?> GetCategoriesAsync(CancellationToken ct);

    Task<ProductListResult?> GetProductsByCategoryAsync(string category, int limit, int skip, CancellationToken ct);
}
