namespace BStore.GraphQL.Api.Caching;

/// <summary>
/// Default <see cref="ICacheFlushService"/> implementation. Delegates to <see cref="ICacheService"/>
/// for the actual eviction; emits human-readable descriptions for audit logging and the GraphQL response.
/// </summary>
public sealed class CacheFlushService(
    ICacheService cache,
    ILogger<CacheFlushService> logger) : ICacheFlushService
{
    public async Task<CacheFlushResult> FlushAsync(
        CacheFlushScope scope,
        int? entityId = null,
        CancellationToken ct = default)
    {
        logger.LogInformation("Cache flush requested: scope={Scope} entityId={EntityId}", scope, entityId);

        switch (scope)
        {
            case CacheFlushScope.All:
                await cache.FlushAsync(CacheLayer.Both, ct);
                return new CacheFlushResult(scope, null, "Flushed L1 and L2 (all tracked keys).", true);

            case CacheFlushScope.L1Only:
                await cache.FlushAsync(CacheLayer.L1, ct);
                return new CacheFlushResult(scope, null, "Flushed L1 (in-memory) only.", true);

            case CacheFlushScope.L2Only:
                await cache.FlushAsync(CacheLayer.L2, ct);
                return new CacheFlushResult(scope, null, "Flushed L2 (Redis / distributed) only.", true);

            case CacheFlushScope.BStores:
                await cache.RemoveByPrefixAsync(CacheKeys.PrefixBStore, ct);
                return new CacheFlushResult(scope, null, "Evicted every bstore:* key.", true);

            case CacheFlushScope.BStore:
                RequireEntityId(scope, entityId);
                await cache.RemoveAsync(CacheKeys.ForBStore(entityId!.Value), ct);
                return new CacheFlushResult(scope, entityId, $"Evicted bstore identity + theme for storeId={entityId}.", true);

            case CacheFlushScope.Users:
                await cache.RemoveByPrefixAsync(CacheKeys.PrefixUser, ct);
                return new CacheFlushResult(scope, null, "Evicted every user:* key.", true);

            case CacheFlushScope.User:
                RequireEntityId(scope, entityId);
                await cache.RemoveAsync(CacheKeys.ForUser(entityId!.Value), ct);
                await cache.RemoveAsync(CacheKeys.ForBStoreUser(entityId.Value), ct);
                return new CacheFlushResult(scope, entityId, $"Evicted user identity + role/access for userId={entityId}.", true);

            case CacheFlushScope.Products:
                await cache.RemoveByPrefixAsync(CacheKeys.PrefixProduct, ct);
                return new CacheFlushResult(scope, null, "Evicted every product:* key.", true);

            case CacheFlushScope.Product:
                RequireEntityId(scope, entityId);
                await cache.RemoveByPrefixAsync($"{CacheKeys.PrefixProduct}{entityId!.Value}", ct);
                return new CacheFlushResult(scope, entityId, $"Evicted every key for productId={entityId} (base/price/inventory/seo/attr).", true);

            case CacheFlushScope.Categories:
                await cache.RemoveByPrefixAsync(CacheKeys.PrefixCategory, ct);
                return new CacheFlushResult(scope, null, "Evicted every catalog:* category-tree key.", true);

            case CacheFlushScope.Attributes:
                await cache.RemoveByPrefixAsync(CacheKeys.PrefixAttr, ct);
                return new CacheFlushResult(scope, null, "Evicted every attr:* attribute-group key.", true);

            case CacheFlushScope.Portal:
                RequireEntityId(scope, entityId);
                foreach (var prefix in CacheKeys.ForPortal(entityId!.Value))
                    await cache.RemoveByPrefixAsync(prefix, ct);
                return new CacheFlushResult(scope, entityId, $"Evicted every portal-scoped key for portalId={entityId}.", true);

            default:
                throw new ArgumentOutOfRangeException(nameof(scope), scope, "Unknown CacheFlushScope.");
        }
    }

    private static void RequireEntityId(CacheFlushScope scope, int? entityId)
    {
        if (entityId is null or <= 0)
            throw new ArgumentException($"Cache flush scope '{scope}' requires a positive entityId.", nameof(entityId));
    }
}
