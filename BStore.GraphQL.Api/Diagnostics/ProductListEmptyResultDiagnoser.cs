using Microsoft.EntityFrameworkCore;
using Znode.Libraries.Data.ZnodeEntity;

namespace BStore.GraphQL.Api.Diagnostics;

/// <summary>
/// Explains why <c>productList</c> / <c>productsByCategory</c> returned no rows (ADR-020).
/// </summary>
public sealed class ProductListEmptyResultDiagnoser(IDbContextFactory<Znode_Entities> dbFactory) : IEmptyResultDiagnoser
{
    public string Operation => "productList";

    public async Task<IReadOnlyList<EmptyResultReason>> DiagnoseAsync(
        IReadOnlyDictionary<string, object?> args,
        CancellationToken ct)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var reasons = new List<EmptyResultReason>(2);

        var hasAnyPublished = await db.ZnodePublishProductDetails.AsNoTracking().AnyAsync(ct);
        if (!hasAnyPublished)
            reasons.Add(new EmptyResultReason(EmptyResultReasons.NoPublishedVersion,
                "No rows in ZnodePublishProductDetail. Run a publish from PIM."));

        if (args.TryGetValue("category", out var cat) && cat is string categoryCode && !string.IsNullOrWhiteSpace(categoryCode))
        {
            var has = await db.ZnodePublishCategoryDetails.AsNoTracking()
                .AnyAsync(c => c.CategoryCode == categoryCode, ct);
            if (!has)
                reasons.Add(new EmptyResultReason(EmptyResultReasons.CategoryNotFound,
                    $"No published category with code '{categoryCode}'."));
        }

        if (args.TryGetValue("q", out var qObj) && qObj is string q && q.Trim().Length < 2)
            reasons.Add(new EmptyResultReason(EmptyResultReasons.SearchTermTooShort,
                "Search term must be at least 2 characters."));

        return reasons;
    }
}
