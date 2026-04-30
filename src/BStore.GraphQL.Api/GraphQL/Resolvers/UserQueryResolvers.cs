using BStore.GraphQL.Api.Caching;
using BStore.GraphQL.Api.Common;
using BStore.GraphQL.Api.Configuration;
using BStore.GraphQL.Api.Data;
using BStore.GraphQL.Api.GraphQL.Queries;
using BStore.GraphQL.Api.GraphQL.Types;
using HotChocolate;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ICacheService = BStore.GraphQL.Api.Caching.ICacheService;

namespace BStore.GraphQL.Api.GraphQL.Resolvers;

/// <summary>User management query fields (type extension on <see cref="BStoreQuery"/>).</summary>
[ExtendObjectType(typeof(BStoreQuery))]
public sealed class UserQueryResolvers(
    ILogger<UserQueryResolvers> logger,
    ICacheService cache,
    IOptions<GraphQLOptions> options)
{
    private readonly GraphQLOptions _opts = options.Value;

    public async Task<UserListPage?> UserList(
        int page,
        int pageSize,
        [Service] IUserDataService users,
        CancellationToken ct)
    {
        page = page < 1 ? 1 : page;
        pageSize = pageSize <= 0 ? 10 : Math.Min(pageSize, _opts.MaxPageSize);
        logger.LogInformation("UserList: page={Page} pageSize={PageSize}", page, pageSize);
        try
        {
            return await users.GetUsersPagedAsync(page, pageSize, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "UserList failed");
            throw ErrorMapper.ToGraphQL(ex);
        }
    }

    public async Task<UserRow?> User(
        int userId,
        [Service] IUserDataService users,
        CancellationToken ct)
    {
        logger.LogInformation("User: userId={UserId}", userId);
        try
        {
            return await cache.GetOrSetAsync(
                CacheKeys.User(userId),
                () => users.GetUserAsync(userId, ct),
                TimeSpan.FromSeconds(_opts.DefaultCacheExpirySeconds),
                ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "User failed: userId={UserId}", userId);
            throw ErrorMapper.ToGraphQL(ex);
        }
    }

    public async Task<UserRow?> UserByUsername(
        string username,
        string storeCode,
        [Service] IUserDataService users,
        CancellationToken ct)
    {
        logger.LogInformation("UserByUsername: username={Username} storeCode={StoreCode}", username, storeCode);
        try
        {
            return await cache.GetOrSetAsync(
                CacheKeys.UserByUsername(username, storeCode),
                () => users.GetUserByUsernameAsync(username, storeCode, ct),
                TimeSpan.FromSeconds(_opts.DefaultCacheExpirySeconds),
                ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "UserByUsername failed: username={Username}", username);
            throw ErrorMapper.ToGraphQL(ex);
        }
    }
}
