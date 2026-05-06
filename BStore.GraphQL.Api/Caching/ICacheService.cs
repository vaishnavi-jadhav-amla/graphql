namespace BStore.GraphQL.Api.Caching;

/// <summary>
/// Thin abstraction over the L1 (in-memory) + L2 (distributed) cache stack.
/// Swap the backing store (in-memory ↔ Redis) by changing the registration in DI without touching resolvers.
/// </summary>
public interface ICacheService
{
    /// <summary>
    /// Returns the cached value for <paramref name="key"/>, or invokes <paramref name="factory"/>,
    /// caches the result, and returns it. Cache misses on the factory call are passed through transparently.
    /// </summary>
    Task<T?> GetOrSetAsync<T>(
        string key,
        Func<Task<T?>> factory,
        TimeSpan? expiry = null,
        CancellationToken ct = default) where T : class;

    /// <summary>Removes a single cache entry. Failures are swallowed and logged — never throws.</summary>
    Task RemoveAsync(string key, CancellationToken ct = default);

    /// <summary>Removes multiple cache entries in sequence (e.g. on mutation invalidation).</summary>
    Task RemoveAsync(IEnumerable<string> keys, CancellationToken ct = default);

    /// <summary>
    /// Removes all tracked cache entries whose keys start with <paramref name="prefix"/>.
    /// Useful for invalidating an entire entity family (e.g. <c>"product:"</c>) or a portal-scoped slice.
    /// </summary>
    Task RemoveByPrefixAsync(string prefix, CancellationToken ct = default);

    /// <summary>
    /// Clears every tracked entry from L1 and (best-effort) L2 for the indicated layers.
    /// Preferred over per-key removal for global resets (cache poisoning, deploy bumps).
    /// </summary>
    Task FlushAsync(CacheLayer layers = CacheLayer.Both, CancellationToken ct = default);
}

/// <summary>Identifies which tier(s) a cache operation should target.</summary>
[Flags]
public enum CacheLayer
{
    None = 0,
    L1 = 1,
    L2 = 2,
    Both = L1 | L2
}
