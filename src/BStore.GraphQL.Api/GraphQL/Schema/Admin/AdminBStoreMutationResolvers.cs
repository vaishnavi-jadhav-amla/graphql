using BStore.GraphQL.Api.Application;
using BStore.GraphQL.Api.Auth;
using BStore.GraphQL.Api.Caching;
using BStore.GraphQL.Api.Common;
using BStore.GraphQL.Api.GraphQL.Types;
using BStore.GraphQL.Api.Messaging;
using HotChocolate;
using HotChocolate.Authorization;
using HotChocolate.Types;
using ICacheService = BStore.GraphQL.Api.Caching.ICacheService;

namespace BStore.GraphQL.Api.GraphQL.Schema.Admin;

/// <summary>
/// Admin B-store mutations — admin-only write operations.
/// All mutations require AdminOnly or BStoreAdmin policy.
/// </summary>
[ExtendObjectType(typeof(AdminMutation))]
public sealed class AdminBStoreMutationResolvers(
    ILogger<AdminBStoreMutationResolvers> logger,
    ICacheService cache,
    IEventPublisher events)
{
    [Authorize(Policy = AuthConstants.PolicyBStoreAdmin)]
    [GraphQLDescription("Set B-store activation status (admin only).")]
    public async Task<bool> BStoreSetActivation(
        int storeId,
        int userId,
        bool isActive,
        [Service] IBStoreApplicationService service,
        CancellationToken ct)
    {
        logger.LogInformation("Admin: BStoreSetActivation storeId={StoreId} isActive={IsActive}", storeId, isActive);
        try
        {
            var result = await service.SetActivationAsync(storeId, userId, isActive, ct);
            if (result)
            {
                await cache.RemoveAsync(CacheKeys.BStore(storeId), ct);
                _ = events.PublishAsync(
                    BStoreGraphQLEventRoutingKeys.BStoreActivationChanged,
                    new { StoreId = storeId, IsActive = isActive },
                    headers: null, ct: ct);
            }
            return result;
        }
        catch (Exception ex)
        {
            if (ex is GraphQLException) throw;
            logger.LogError(ex, "Admin: BStoreSetActivation failed: storeId={StoreId}", storeId);
            throw ErrorMapper.ToGraphQL(ex);
        }
    }
}
