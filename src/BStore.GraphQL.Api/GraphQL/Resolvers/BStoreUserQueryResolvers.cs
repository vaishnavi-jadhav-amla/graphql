using BStore.GraphQL.Api.Auth.FieldPermissions;
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

/// <summary>B-store user access / role query fields (type extension on <see cref="BStoreQuery"/>).</summary>
[ExtendObjectType(typeof(BStoreQuery))]
public sealed class BStoreUserQueryResolvers(
    ILogger<BStoreUserQueryResolvers> logger,
    ICacheService cache,
    IOptions<GraphQLOptions> options)
{
    private readonly GraphQLOptions _opts = options.Value;

    [GraphQLDescription("B-store manager/owner flags from AspNet roles (Znode_Entities).")]
    [RequireBStoreAdmin]
    public async Task<BStoreUserRoleRow?> BStoreUserRoleAccess(
        int userId,
        [Service] IBStoreUserDataService users,
        CancellationToken ct)
    {
        logger.LogInformation("BStoreUserRoleAccess: userId={UserId}", userId);
        try
        {
            return await cache.GetOrSetAsync(
                CacheKeys.BStoreUserRoleAccess(userId),
                () => users.GetUserRoleAccessAsync(userId, ct),
                TimeSpan.FromSeconds(_opts.DefaultCacheExpirySeconds),
                ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "BStoreUserRoleAccess failed: userId={UserId}", userId);
            throw ErrorMapper.ToGraphQL(ex);
        }
    }

    [GraphQLDescription("Associated or unassociated B-store portals for a user (EF).")]
    [RequireBStoreAdmin]
    public async Task<BStoreUserAccessListResult?> BStoreUserAccessList(
        int userId,
        bool isAssociated,
        int pageIndex,
        int pageSize,
        [Service] IBStoreUserDataService users,
        CancellationToken ct)
    {
        pageIndex = Math.Max(pageIndex, 1);
        pageSize  = pageSize <= 0 ? 10 : Math.Min(pageSize, _opts.MaxPageSize);

        logger.LogInformation(
            "BStoreUserAccessList: userId={UserId} isAssociated={IsAssociated} page={Page}/{Size}",
            userId, isAssociated, pageIndex, pageSize);

        try
        {
            return await cache.GetOrSetAsync(
                CacheKeys.BStoreUserAccessList(userId, isAssociated, pageIndex, pageSize),
                () => users.GetUserAccessListAsync(userId, isAssociated, pageIndex, pageSize, ct),
                TimeSpan.FromSeconds(_opts.ListCacheExpirySeconds),
                ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "BStoreUserAccessList failed: userId={UserId}", userId);
            throw ErrorMapper.ToGraphQL(ex);
        }
    }
}
