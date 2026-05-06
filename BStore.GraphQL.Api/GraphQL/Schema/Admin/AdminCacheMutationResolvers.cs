using BStore.GraphQL.Api.Auth;
using BStore.GraphQL.Api.Caching;
using BStore.GraphQL.Api.Common;
using HotChocolate;
using HotChocolate.Authorization;
using HotChocolate.Types;
using ErrorCodes = BStore.GraphQL.Api.Common.ErrorCodes;

namespace BStore.GraphQL.Api.GraphQL.Schema.Admin;

/// <summary>
/// Admin cache-control mutations. Exposes a single, scoped <c>flushCaches</c> mutation
/// covering twelve flush modes (see <see cref="CacheFlushScope"/>) so admins can target
/// the smallest possible eviction without bouncing every key in the cluster.
/// </summary>
[ExtendObjectType(typeof(AdminMutation))]
public sealed class AdminCacheMutationResolvers(
    ILogger<AdminCacheMutationResolvers> logger)
{
    /// <summary>
    /// Flush cache entries by scope. Twelve scopes are supported:
    /// <c>ALL</c>, <c>L1_ONLY</c>, <c>L2_ONLY</c>, <c>BSTORES</c>, <c>BSTORE</c>,
    /// <c>USERS</c>, <c>USER</c>, <c>PRODUCTS</c>, <c>PRODUCT</c>,
    /// <c>CATEGORIES</c>, <c>ATTRIBUTES</c>, <c>PORTAL</c>.
    /// </summary>
    /// <param name="scope">Eviction granularity.</param>
    /// <param name="entityId">
    /// Required for single-entity scopes (<c>BSTORE</c>, <c>USER</c>, <c>PRODUCT</c>, <c>PORTAL</c>);
    /// ignored otherwise.
    /// </param>
    [Authorize(Policy = AuthConstants.PolicyAdminOnly)]
    [GraphQLDescription("Flush cache entries by scope. Twelve modes ranging from a global flush down to single-entity eviction.")]
    public async Task<CacheFlushPayload> FlushCaches(
        CacheFlushScope scope,
        int? entityId,
        [Service] ICacheFlushService flushService,
        CancellationToken ct)
    {
        logger.LogInformation("Admin flushCaches: scope={Scope} entityId={EntityId}", scope, entityId);
        try
        {
            var result = await flushService.FlushAsync(scope, entityId, ct);
            return new CacheFlushPayload(
                Scope: result.Scope,
                EntityId: result.EntityId,
                Description: result.Description,
                Broadcasted: result.Broadcasted,
                FlushedAt: DateTimeOffset.UtcNow);
        }
        catch (ArgumentException ex)
        {
            throw new GraphQLException(ErrorBuilder.New()
                .SetMessage(ex.Message)
                .SetCode(ErrorCodes.Validation)
                .Build());
        }
        catch (Exception ex)
        {
            if (ex is GraphQLException) throw;
            logger.LogError(ex, "flushCaches failed: scope={Scope} entityId={EntityId}", scope, entityId);
            throw ErrorMapper.ToGraphQL(ex);
        }
    }
}

/// <summary>Result envelope returned by <c>flushCaches</c>.</summary>
public sealed record CacheFlushPayload(
    CacheFlushScope Scope,
    int? EntityId,
    string Description,
    bool Broadcasted,
    DateTimeOffset FlushedAt);
