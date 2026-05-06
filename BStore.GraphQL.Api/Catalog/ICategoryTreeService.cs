namespace BStore.GraphQL.Api.Catalog;

/// <summary>
/// ADR-015: category trees are loaded via materialized path / depth ordering, not recursive CTE.
/// </summary>
public interface ICategoryTreeService
{
    Task<IReadOnlyList<CategoryNode>> GetTreeAsync(int catalogId, string locale, CancellationToken ct);
}

public sealed record CategoryNode(
    int CategoryId,
    int? ParentCategoryId,
    string Code,
    string Name,
    string Path,
    int Depth,
    int Order);
