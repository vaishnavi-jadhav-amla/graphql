using BStore.GraphQL.Api.Application;
using BStore.GraphQL.Api.Auth;
using BStore.GraphQL.Api.Caching;
using BStore.GraphQL.Api.Configuration;
using BStore.GraphQL.Api.GraphQL.Types;
using HotChocolate;
using HotChocolate.Authorization;
using HotChocolate.Types;
using Microsoft.Extensions.Options;
using ICacheService = BStore.GraphQL.Api.Caching.ICacheService;

namespace BStore.GraphQL.Api.GraphQL.Schema.Admin;

/// <summary>
/// Admin B-store queries — admin-only operations for B-store management.
/// </summary>
[ExtendObjectType(typeof(AdminQuery))]
public sealed class AdminBStoreQueryResolvers
{
    [Authorize(Policy = AuthConstants.PolicyAdminOnly)]
    [GraphQLDescription("Get B-store details by id (admin only).")]
    public async Task<BStoreDetails?> BStoreById(
        int storeId,
        [Service] IBStoreApplicationService service,
        [Service] ICacheService cache,
        [Service] IOptions<GraphQLOptions> options,
        CancellationToken ct)
    {
        var key = CacheKeys.BStore(storeId);
        var ttl = TimeSpan.FromSeconds(options.Value.DefaultCacheExpirySeconds);
        return await cache.GetOrSetAsync(key,
            () => service.GetStoreDetailsAsync(storeId, ct),
            ttl, ct);
    }
}
