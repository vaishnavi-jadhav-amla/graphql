using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO.Compression;
using System.Text.Json;
using BStore.GraphQL.Api.Configuration;
using BStore.GraphQL.Api.Diagnostics;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace BStore.GraphQL.Api.Caching;

/// <summary>
/// L1: <see cref="IMemoryCache"/> (fast, per process). L2: <see cref="IDistributedCache"/> (Redis or memory).
/// On miss: factory → L2 → L1. Invalidations clear both layers.
/// Includes stampede protection via per-key semaphore locking to prevent thundering herd.
/// </summary>
public sealed class LayeredCacheService(
    IMemoryCache memory,
    IDistributedCache distributed,
    IOptions<CachingOptions> cachingOptions,
    IProviderHealthTracker providerHealth,
    ILogger<LayeredCacheService> logger) : ICacheService
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private readonly TimeSpan _l1Cap = TimeSpan.FromSeconds(Math.Max(1, cachingOptions.Value.L1MaxEntrySeconds));
    private readonly bool _compressL2 = cachingOptions.Value.CompressL2Payloads;
    private readonly int  _compressMinBytes = Math.Max(0, cachingOptions.Value.CompressL2MinBytes);

    // Stampede protection: one semaphore per cache key prevents concurrent factory calls for the same key.
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> KeyLocks = new();
    private readonly HashSet<string> _trackedKeys = new(StringComparer.Ordinal);
    private readonly object _keyTrackingGate = new();

    public async Task<T?> GetOrSetAsync<T>(
        string key,
        Func<Task<T?>> factory,
        TimeSpan? expiry = null,
        CancellationToken ct = default) where T : class
    {
        // Fast path: L1 hit (no lock needed)
        if (memory.TryGetValue(key, out object? cached) && cached is T typed)
        {
            logger.LogDebug("L1 cache hit: {Key}", key);
            return typed;
        }

        // Stampede protection: acquire a per-key lock so only one caller runs the factory
        var keyLock = KeyLocks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
        await keyLock.WaitAsync(ct);
        try
        {
            // Double-check L1 after acquiring lock (another thread may have populated it)
            if (memory.TryGetValue(key, out cached) && cached is T typed2)
            {
                logger.LogDebug("L1 cache hit (post-lock): {Key}", key);
                return typed2;
            }

            var sw = Stopwatch.StartNew();
            try
            {
                var bytes = await distributed.GetAsync(key, ct);
                sw.Stop();
                providerHealth.Record(DataSource.CacheL2, success: true, sw.ElapsedMilliseconds);
                if (bytes is { Length: > 0 })
                {
                    var fromL2 = DeserializePayload<T>(bytes);
                    if (fromL2 is not null)
                    {
                        logger.LogDebug("L2 cache hit: {Key}", key);
                        SetL1(key, fromL2, expiry);
                        TrackKey(key);
                        return fromL2;
                    }
                }
            }
            catch (Exception ex)
            {
                sw.Stop();
                providerHealth.Record(DataSource.CacheL2, success: false, sw.ElapsedMilliseconds, ex.Message);
                logger.LogWarning(ex, "L2 cache read failed for {Key}; continuing without L2", key);
            }

            var value = await factory();
            if (value is null)
                return null;

            await SetL2Async(key, value, expiry, ct);
            SetL1(key, value, expiry);
            TrackKey(key);
            return value;
        }
        finally
        {
            keyLock.Release();
            // Clean up semaphore if no one else is waiting (prevent memory leak)
            if (keyLock.CurrentCount == 1)
                KeyLocks.TryRemove(key, out _);
        }
    }

    public async Task RemoveAsync(string key, CancellationToken ct = default)
    {
        memory.Remove(key);
        try
        {
            await distributed.RemoveAsync(key, ct);
            logger.LogDebug("Cache invalidated (L1+L2): {Key}", key);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "L2 cache remove failed for {Key}", key);
        }
    }

    public async Task RemoveAsync(IEnumerable<string> keys, CancellationToken ct = default)
    {
        foreach (var key in keys)
            await RemoveAsync(key, ct);
    }

    private void SetL1<T>(string key, T value, TimeSpan? expiry) where T : class
    {
        var l1Ttl = CapL1Ttl(expiry);
        memory.Set(key, value, new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = l1Ttl });
    }

    private TimeSpan CapL1Ttl(TimeSpan? expiry)
    {
        var requested = expiry ?? TimeSpan.FromSeconds(60);
        return requested < _l1Cap ? requested : _l1Cap;
    }

    private async Task SetL2Async<T>(string key, T value, TimeSpan? expiry, CancellationToken ct) where T : class
    {
        try
        {
            var payload = SerializePayload(value);
            await distributed.SetAsync(
                key,
                payload,
                new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = expiry ?? TimeSpan.FromSeconds(60)
                },
                ct);
            logger.LogDebug("L2 cache set: {Key}", key);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "L2 cache write failed for {Key}", key);
        }
    }

    private byte[] SerializePayload<T>(T value) where T : class
    {
        var json = JsonSerializer.SerializeToUtf8Bytes(value, SerializerOptions);
        if (!_compressL2 || json.Length < _compressMinBytes)
            return json;

        using var ms = new MemoryStream();
        using (var gz = new GZipStream(ms, CompressionLevel.Fastest, leaveOpen: true))
            gz.Write(json);

        var compressed = ms.ToArray();
        if (compressed.Length >= json.Length)
            return json;

        var marker = new byte[compressed.Length + 1];
        marker[0] = 0x1F;
        compressed.AsSpan().CopyTo(marker.AsSpan(1));
        return marker;
    }

    private T? DeserializePayload<T>(byte[] bytes) where T : class
    {
        try
        {
            if (bytes.Length > 0 && bytes[0] == 0x1F)
            {
                using var input = new MemoryStream(bytes, 1, bytes.Length - 1);
                using var gz = new GZipStream(input, CompressionMode.Decompress);
                using var output = new MemoryStream();
                gz.CopyTo(output);
                return JsonSerializer.Deserialize<T>(output.ToArray(), SerializerOptions);
            }

            return JsonSerializer.Deserialize<T>(bytes, SerializerOptions);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Cache payload deserialize failed");
            return null;
        }
    }

    /// <summary>Track a key for prefix-based invalidation.</summary>
    private void TrackKey(string key)
    {
        lock (_keyTrackingGate) _trackedKeys.Add(key);
    }

    /// <summary>
    /// Remove all cache entries whose keys start with the given prefix.
    /// Useful for invalidating all entries for a specific entity (e.g. "bstore:123:").
    /// </summary>
    public async Task RemoveByPrefixAsync(string prefix, CancellationToken ct = default)
    {
        string[] matchingKeys;
        lock (_keyTrackingGate)
        {
            matchingKeys = _trackedKeys
                .Where(k => k.StartsWith(prefix, StringComparison.Ordinal))
                .ToArray();

            foreach (var key in matchingKeys)
                _trackedKeys.Remove(key);
        }

        foreach (var key in matchingKeys)
            await RemoveAsync(key, ct);

        logger.LogDebug("Prefix invalidation: {Prefix} removed {Count} keys", prefix, matchingKeys.Length);
    }
}
