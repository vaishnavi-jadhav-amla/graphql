using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace BStore.GraphQL.Api.Caching;

/// <summary>
/// <see cref="ICacheService"/> backed by <see cref="IDistributedCache"/>.
/// Works with <c>AddDistributedMemoryCache()</c> (dev/test) or
/// <c>AddStackExchangeRedisCache()</c> (production) without any code change.
/// All cache errors are caught and logged — the service never throws, ensuring
/// that a Redis outage degrades gracefully to a live read.
/// </summary>
public sealed class DistributedCacheService(
    IDistributedCache cache,
    ILogger<DistributedCacheService> logger) : ICacheService
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private static readonly TimeSpan DefaultExpiry = TimeSpan.FromSeconds(60);

    public async Task<T?> GetOrSetAsync<T>(
        string key,
        Func<Task<T?>> factory,
        TimeSpan? expiry = null,
        CancellationToken ct = default) where T : class
    {
        // ── 1. Cache read ──────────────────────────────────────────────────
        try
        {
            var bytes = await cache.GetAsync(key, ct);
            if (bytes is { Length: > 0 })
            {
                logger.LogDebug("Cache hit: {CacheKey}", key);
                return JsonSerializer.Deserialize<T>(bytes, SerializerOptions);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Cache read failed for {CacheKey}; falling back to source", key);
        }

        // ── 2. Source read ─────────────────────────────────────────────────
        var value = await factory();

        // ── 3. Cache write (best-effort) ───────────────────────────────────
        if (value is not null)
        {
            try
            {
                var entry = JsonSerializer.SerializeToUtf8Bytes(value, SerializerOptions);
                await cache.SetAsync(
                    key,
                    entry,
                    new DistributedCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = expiry ?? DefaultExpiry
                    },
                    ct);
                logger.LogDebug("Cache set: {CacheKey} (expiry {Expiry}s)", key, (expiry ?? DefaultExpiry).TotalSeconds);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Cache write failed for {CacheKey}; result still returned", key);
            }
        }

        return value;
    }

    public async Task RemoveAsync(string key, CancellationToken ct = default)
    {
        try
        {
            await cache.RemoveAsync(key, ct);
            logger.LogDebug("Cache invalidated: {CacheKey}", key);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Cache remove failed for {CacheKey}", key);
        }
    }

    public async Task RemoveAsync(IEnumerable<string> keys, CancellationToken ct = default)
    {
        foreach (var key in keys)
            await RemoveAsync(key, ct);
    }
}
