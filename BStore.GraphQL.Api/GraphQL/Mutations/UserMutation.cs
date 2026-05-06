using BStore.GraphQL.Api.Auth.FieldPermissions;
using BStore.GraphQL.Api.Caching;
using BStore.GraphQL.Api.Common;
using BStore.GraphQL.Api.Data;
using BStore.GraphQL.Api.GraphQL.Types;
using HotChocolate;
using Microsoft.Extensions.Logging;
using ICacheService = BStore.GraphQL.Api.Caching.ICacheService;

namespace BStore.GraphQL.Api.GraphQL.Mutations;

/// <summary>
/// User management mutations (Znode_Entities where supported; no HTTP).
/// Extends the main <see cref="BStoreMutation"/> root.
/// </summary>
[ExtendObjectType(typeof(BStoreMutation))]
public sealed class UserMutation(
    ILogger<UserMutation> logger,
    ICacheService cache)
{
    [RequireAdmin]
    public async Task<UserRow?> UserCreate(
        UserCreateInput input,
        [Service] IUserDataService users,
        CancellationToken ct)
    {
        logger.LogInformation("UserCreate: email={Email}", input.Email);
        try
        {
            return await users.CreateUserAsync(input, ct);
        }
        catch (Exception ex)
        {
            if (ex is GraphQLException) throw;
            logger.LogError(ex, "UserCreate failed: email={Email}", input.Email);
            throw ErrorMapper.ToGraphQL(ex);
        }
    }

    [RequireAdmin]
    public async Task<bool> UserDelete(
        string id,
        [Service] IUserDataService users,
        CancellationToken ct)
    {
        logger.LogInformation("UserDelete: id={Id}", id);
        try
        {
            return await users.DeleteUserAsync(id, ct);
        }
        catch (Exception ex)
        {
            if (ex is GraphQLException) throw;
            logger.LogError(ex, "UserDelete failed: id={Id}", id);
            throw ErrorMapper.ToGraphQL(ex);
        }
    }

    [RequireAuthenticated]
    public async Task<bool> UserUpdate(
        UserUpdateInput input,
        [Service] IUserDataService users,
        CancellationToken ct)
    {
        logger.LogInformation("UserUpdate: userId={UserId}", input.UserId);
        try
        {
            var result = await users.UpdateUserAsync(input, ct);
            if (result)
                await cache.RemoveAsync(CacheKeys.User(input.UserId), ct);
            return result;
        }
        catch (Exception ex)
        {
            if (ex is GraphQLException) throw;
            logger.LogError(ex, "UserUpdate failed: userId={UserId}", input.UserId);
            throw ErrorMapper.ToGraphQL(ex);
        }
    }

    [RequireAdmin]
    public async Task<bool> UserToggleActive(
        int userId,
        bool lockUser,
        [Service] IUserDataService users,
        CancellationToken ct)
    {
        logger.LogInformation("UserToggleActive: userId={UserId} lockUser={LockUser}", userId, lockUser);
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
            logger.LogError(ex, "UserToggleActive failed: userId={UserId}", userId);
            throw ErrorMapper.ToGraphQL(ex);
        }
    }
}
