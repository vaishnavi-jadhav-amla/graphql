using BStore.GraphQL.Api.Auth;
using BStore.GraphQL.Api.Caching;
using BStore.GraphQL.Api.Common;
using BStore.GraphQL.Api.Data;
using BStore.GraphQL.Api.GraphQL.Types;
using HotChocolate;
using HotChocolate.Authorization;
using HotChocolate.Types;
using ICacheService = BStore.GraphQL.Api.Caching.ICacheService;

namespace BStore.GraphQL.Api.GraphQL.Schema.Admin;

/// <summary>
/// Admin user management mutations — create, update, delete, toggle users.
/// All mutations require AdminOnly policy.
/// </summary>
[ExtendObjectType(typeof(AdminMutation))]
public sealed class AdminUserMutationResolvers(
    ILogger<AdminUserMutationResolvers> logger,
    ICacheService cache)
{
    [Authorize(Policy = AuthConstants.PolicyAdminOnly)]
    [GraphQLDescription("Create a new user (admin only).")]
    public async Task<UserRow?> UserCreate(
        UserCreateInput input,
        [Service] IUserDataService users,
        CancellationToken ct)
    {
        logger.LogInformation("Admin: UserCreate email={Email}", input.Email);
        try
        {
            return await users.CreateUserAsync(input, ct);
        }
        catch (Exception ex)
        {
            if (ex is GraphQLException) throw;
            logger.LogError(ex, "Admin: UserCreate failed: email={Email}", input.Email);
            throw ErrorMapper.ToGraphQL(ex);
        }
    }

    [Authorize(Policy = AuthConstants.PolicyAdminOnly)]
    [GraphQLDescription("Toggle user active/locked status (admin only).")]
    public async Task<bool> UserToggleActive(
        int userId,
        bool lockUser,
        [Service] IUserDataService users,
        CancellationToken ct)
    {
        logger.LogInformation("Admin: UserToggleActive userId={UserId} lock={Lock}", userId, lockUser);
        try
        {
            var result = await users.ToggleUserActiveAsync(userId, lockUser, ct);
            if (result)
                await cache.RemoveAsync(CacheKeys.User(userId), ct);
            return result;
        }
        catch (Exception ex)
        {
            if (ex is GraphQLException) throw;
            logger.LogError(ex, "Admin: UserToggleActive failed: userId={UserId}", userId);
            throw ErrorMapper.ToGraphQL(ex);
        }
    }
}
