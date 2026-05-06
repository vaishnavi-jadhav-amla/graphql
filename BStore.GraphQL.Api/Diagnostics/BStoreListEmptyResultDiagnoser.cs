using Microsoft.EntityFrameworkCore;
using Znode.Libraries.Data.ZnodeEntity;

namespace BStore.GraphQL.Api.Diagnostics;

/// <summary>Explains why <c>bStoreList(portalId, userId)</c> returned no rows (ADR-020).</summary>
public sealed class BStoreListEmptyResultDiagnoser(IDbContextFactory<Znode_Entities> dbFactory) : IEmptyResultDiagnoser
{
    public string Operation => "bStoreList";

    public async Task<IReadOnlyList<EmptyResultReason>> DiagnoseAsync(
        IReadOnlyDictionary<string, object?> args,
        CancellationToken ct)
    {
        if (!args.TryGetValue("portalId", out var pidObj) || pidObj is not int portalId)
            return Array.Empty<EmptyResultReason>();

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        // Server-side projection: only the columns we need.
        var portal = await db.ZnodePortals.AsNoTracking()
            .Where(p => p.PortalId == portalId)
            .Select(p => new { p.PortalId, p.IsActive })
            .FirstOrDefaultAsync(ct);

        var reasons = new List<EmptyResultReason>(2);

        if (portal is null)
        {
            reasons.Add(new EmptyResultReason(EmptyResultReasons.PortalNotFound,
                $"Parent portal {portalId} does not exist."));
            return reasons;
        }

        if (!portal.IsActive)
            reasons.Add(new EmptyResultReason(EmptyResultReasons.PortalInactive,
                $"Parent portal {portalId} is inactive."));

        return reasons;
    }
}
