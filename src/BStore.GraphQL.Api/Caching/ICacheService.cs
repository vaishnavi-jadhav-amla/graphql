namespace BStore.GraphQL.Api.Caching;

/// <summary>
/// Thin abstraction over <see cref="Microsoft.Extensions.Caching.Distributed.IDistributedCache"/>.
/// Swap the backing store (in-memory ↔ Redis) by changing the registration in DI without touching resolvers.
/// </summary>
public interface ICacheService
{
    /// <summary>
    /// Returns the cached value for <paramref name="key"/>, or invokes <paramref name="factory"/>,
    /// caches the result, and returns it.  Cache misses on the factory call are passed through transparently.
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
}
