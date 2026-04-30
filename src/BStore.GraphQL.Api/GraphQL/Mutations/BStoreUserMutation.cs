using BStore.GraphQL.Api.Caching;
using BStore.GraphQL.Api.Common;
using BStore.GraphQL.Api.Data;
using BStore.GraphQL.Api.GraphQL.Types;
using HotChocolate;
using Microsoft.Extensions.Logging;
using ICacheService = BStore.GraphQL.Api.Caching.ICacheService;

namespace BStore.GraphQL.Api.GraphQL.Mutations;

/// <summary>
/// Mutations for B-store user access and role management (EF / stored procedures; no HTTP).
/// Extends the main <see cref="BStoreMutation"/> root.
/// </summary>
[ExtendObjectType(typeof(BStoreMutation))]
public sealed class BStoreUserMutation(
    ILogger<BStoreUserMutation> logger,
    ICacheService cache)
{
    // [Authorize]
    public async Task<bool> BStoreUserRoleAccessSave(
        BStoreUserRoleInput input,
        [Service] IBStoreUserDataService users,
        CancellationToken ct)
    {
        logger.LogInformation(
            "BStoreUserRoleAccessSave: userId={UserId} isManager={IsManager} isOwner={IsOwner}",
            input.UserId, input.IsManager, input.IsOwner);
        try
        {
            var result = await users.SaveUserRoleAccessAsync(input, ct);
            if (result)
                await cache.RemoveAsync(CacheKeys.BStoreUserRoleAccess(input.UserId), ct);
            return result;
        }
        catch (Exception ex)
        {
            if (ex is GraphQLException) throw;
            logger.LogError(ex, "BStoreUserRoleAccessSave failed: userId={UserId}", input.UserId);
            throw ErrorMapper.ToGraphQL(ex);
        }
    }

    // [Authorize]
    public async Task<bool> BStoreUserAccessToggle(
        BStoreUserAccessInput input,
        [Service] IBStoreUserDataService users,
        CancellationToken ct)
    {
        logger.LogInformation(
            "BStoreUserAccessToggle: userId={UserId} isAssociate={IsAssociate} portals={Portals}",
            input.UserId, input.IsAssociate, string.Join(",", input.PortalIds));
        try
        {
            var result = await users.ToggleUserAccessAsync(input, ct);
            if (result)
            {
                var keys = new[]
                {
                    CacheKeys.BStoreUserAccessList(input.UserId, true,  1, 10),
                    CacheKeys.BStoreUserAccessList(input.UserId, false, 1, 10)
                };
                await cache.RemoveAsync(keys, ct);
            }
            return result;
        }
        catch (Exception ex)
        {
            if (ex is GraphQLException) throw;
            logger.LogError(ex, "BStoreUserAccessToggle failed: userId={UserId}", input.UserId);
            throw ErrorMapper.ToGraphQL(ex);
        }
    }
}
