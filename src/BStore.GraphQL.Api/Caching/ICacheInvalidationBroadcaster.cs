namespace BStore.GraphQL.Api.Caching;

/// <summary>
/// Broadcasts L1-cache invalidation messages to peer instances over Redis Pub/Sub.
/// In single-instance / non-Redis deployments a no-op implementation is registered.
/// </summary>
public interface ICacheInvalidationBroadcaster
{
    /// <summary>Notify peers to drop a single key from their L1.</summary>
    Task PublishKeyInvalidationAsync(string key, CancellationToken ct = default);

    /// <summary>Notify peers to drop every key starting with <paramref name="prefix"/> from their L1.</summary>
    Task PublishPrefixInvalidationAsync(string prefix, CancellationToken ct = default);

    /// <summary>Notify peers to flush the indicated cache layers.</summary>
    Task PublishFlushAsync(CacheLayer layers, CancellationToken ct = default);
}

/// <summary>No-op broadcaster used when Pub/Sub is disabled or Redis is not configured.</summary>
public sealed class NullCacheInvalidationBroadcaster : ICacheInvalidationBroadcaster
{
    public Task PublishKeyInvalidationAsync(string key, CancellationToken ct = default) => Task.CompletedTask;
    public Task PublishPrefixInvalidationAsync(string prefix, CancellationToken ct = default) => Task.CompletedTask;
    public Task PublishFlushAsync(CacheLayer layers, CancellationToken ct = default) => Task.CompletedTask;
}
