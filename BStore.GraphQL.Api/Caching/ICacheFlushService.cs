namespace BStore.GraphQL.Api.Caching;

/// <summary>
/// Coordinates scoped cache flushes across the L1+L2 layered cache. Used by the admin
/// <c>flushCaches</c> mutation and any internal background job that needs to evict by scope.
/// </summary>
public interface ICacheFlushService
{
    /// <summary>
    /// Flush the cache according to <paramref name="scope"/>. <paramref name="entityId"/> is
    /// required for <see cref="CacheFlushScope.BStore"/>, <see cref="CacheFlushScope.User"/>,
    /// <see cref="CacheFlushScope.Product"/> and <see cref="CacheFlushScope.Portal"/>.
    /// </summary>
    /// <returns>Human-readable description of what was flushed (for audit / response).</returns>
    Task<CacheFlushResult> FlushAsync(CacheFlushScope scope, int? entityId = null, CancellationToken ct = default);
}

/// <summary>Outcome of a scoped flush — returned by <see cref="ICacheFlushService"/>.</summary>
public sealed record CacheFlushResult(
    CacheFlushScope Scope,
    int? EntityId,
    string Description,
    bool Broadcasted);
