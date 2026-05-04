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
/// On miss: factory → L2 → L1. Invalidations clear both layers and (when configured) broadcast to peers
/// over Redis Pub/Sub so other instances drop their L1 copy.
/// <para>
/// Stampede protection: per-key <see cref="SemaphoreSlim"/> ensures only one caller runs the factory
/// for a given cache key.
/// </para>
/// <para>
/// Stale-while-revalidate: when an entry crosses its soft TTL (default 80% of full TTL), the
/// stale value is returned immediately and a background refresh kicks off so the next reader gets
/// a fresh value with no extra latency.
/// </para>
/// </summary>
public sealed class LayeredCacheService : ICacheService
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    // Stampede protection: one semaphore per cache key prevents concurrent factory calls for the same key.
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> KeyLocks = new();

    private readonly IMemoryCache _memory;
    private readonly IDistributedCache _distributed;
    private readonly IProviderHealthTracker _providerHealth;
    private readonly ILogger<LayeredCacheService> _logger;
    private readonly ICacheInvalidationBroadcaster _broadcaster;

    private readonly TimeSpan _l1Cap;
    private readonly bool _compressL2;
    private readonly int _compressMinBytes;
    private readonly bool _swrEnabled;
    private readonly double _swrRatio;

    // Tracking layer: keep the set of keys currently in the cache so prefix-eviction and full flush work.
    // Each tracked entry stores the soft TTL deadline so SWR can decide when to background-refresh.
    private readonly ConcurrentDictionary<string, CacheEntryMeta> _tracked = new(StringComparer.Ordinal);

    // Active background refreshes — one per key — to avoid duplicate work while we are stale.
    private readonly ConcurrentDictionary<string, byte> _refreshing = new(StringComparer.Ordinal);

    public LayeredCacheService(
        IMemoryCache memory,
        IDistributedCache distributed,
        IOptions<CachingOptions> cachingOptions,
        IProviderHealthTracker providerHealth,
        ILogger<LayeredCacheService> logger,
        ICacheInvalidationBroadcaster broadcaster)
    {
        _memory          = memory;
        _distributed     = distributed;
        _providerHealth  = providerHealth;
        _logger          = logger;
        _broadcaster     = broadcaster;

        var opts = cachingOptions.Value;
        _l1Cap            = TimeSpan.FromSeconds(Math.Max(1, opts.L1MaxEntrySeconds));
        _compressL2       = opts.CompressL2Payloads;
        _compressMinBytes = Math.Max(0, opts.CompressL2MinBytes);
        _swrEnabled       = opts.EnableStaleWhileRevalidate;
        _swrRatio         = Math.Clamp(opts.StaleAfterRatio, 0.1, 1.0);
    }

    public async Task<T?> GetOrSetAsync<T>(
        string key,
        Func<Task<T?>> factory,
        TimeSpan? expiry = null,
        CancellationToken ct = default) where T : class
    {
        var fullTtl = expiry ?? TimeSpan.FromSeconds(60);

        // Fast path: L1 hit (no lock needed)
        if (_memory.TryGetValue(key, out object? cached) && cached is T typed)
        {
            _logger.LogDebug("L1 cache hit: {Key}", key);
            MaybeBackgroundRefresh(key, factory, fullTtl);
            return typed;
        }

        // Stampede protection: acquire a per-key lock so only one caller runs the factory
        var keyLock = KeyLocks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
        await keyLock.WaitAsync(ct);
        try
        {
            // Double-check L1 after acquiring lock (another thread may have populated it)
            if (_memory.TryGetValue(key, out cached) && cached is T typed2)
            {
                _logger.LogDebug("L1 cache hit (post-lock): {Key}", key);
                MaybeBackgroundRefresh(key, factory, fullTtl);
                return typed2;
            }

            var sw = Stopwatch.StartNew();
            try
            {
                var bytes = await _distributed.GetAsync(key, ct);
                sw.Stop();
                _providerHealth.Record(DataSource.CacheL2, success: true, sw.ElapsedMilliseconds);
                if (bytes is { Length: > 0 })
                {
                    var fromL2 = DeserializePayload<T>(bytes);
                    if (fromL2 is not null)
                    {
                        _logger.LogDebug("L2 cache hit: {Key}", key);
                        SetL1(key, fromL2, fullTtl);
                        Track(key, fullTtl);
                        return fromL2;
                    }
                }
            }
            catch (Exception ex)
            {
                sw.Stop();
                _providerHealth.Record(DataSource.CacheL2, success: false, sw.ElapsedMilliseconds, ex.Message);
                _logger.LogWarning(ex, "L2 cache read failed for {Key}; continuing without L2", key);
            }

            var value = await factory();
            if (value is null)
                return null;

            await SetL2Async(key, value, fullTtl, ct);
            SetL1(key, value, fullTtl);
            Track(key, fullTtl);
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
        RemoveLocal(key);
        try
        {
            await _distributed.RemoveAsync(key, ct);
            _logger.LogDebug("Cache invalidated (L1+L2): {Key}", key);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "L2 cache remove failed for {Key}", key);
        }

        await _broadcaster.PublishKeyInvalidationAsync(key, ct);
    }

    public async Task RemoveAsync(IEnumerable<string> keys, CancellationToken ct = default)
    {
        foreach (var key in keys)
            await RemoveAsync(key, ct);
    }

    public async Task RemoveByPrefixAsync(string prefix, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(prefix))
            return;

        var matchingKeys = _tracked.Keys
            .Where(k => k.StartsWith(prefix, StringComparison.Ordinal))
            .ToArray();

        foreach (var key in matchingKeys)
            await RemoveAsync(key, ct);

        // Even if no L1 keys matched (cold instance), broadcast the prefix so peers can drop theirs.
        await _broadcaster.PublishPrefixInvalidationAsync(prefix, ct);

        _logger.LogInformation(
            "Prefix invalidation: {Prefix} removed {Count} local keys (broadcast to peers)",
            prefix, matchingKeys.Length);
    }

    public async Task FlushAsync(CacheLayer layers = CacheLayer.Both, CancellationToken ct = default)
    {
        if (layers == CacheLayer.None)
            return;

        if (layers.HasFlag(CacheLayer.L1))
        {
            // IMemoryCache doesn't expose Clear(); evict every tracked key.
            foreach (var key in _tracked.Keys.ToArray())
                _memory.Remove(key);
        }

        if (layers.HasFlag(CacheLayer.L2))
        {
            // IDistributedCache lacks a portable "FLUSHDB"; remove every tracked key best-effort.
            foreach (var key in _tracked.Keys.ToArray())
            {
                try { await _distributed.RemoveAsync(key, ct); }
                catch (Exception ex) { _logger.LogWarning(ex, "L2 flush remove failed for {Key}", key); }
            }
        }

        if (layers == CacheLayer.Both || layers == CacheLayer.L1)
            _tracked.Clear();

        await _broadcaster.PublishFlushAsync(layers, ct);

        _logger.LogInformation("Cache flush requested for layers={Layers}", layers);
    }

    /// <summary>
    /// Remove an entry from this instance's L1 and tracking only. Used by the pub/sub
    /// subscriber when a peer broadcasts an invalidation; we must NOT re-broadcast.
    /// </summary>
    internal void RemoveLocal(string key)
    {
        _memory.Remove(key);
        _tracked.TryRemove(key, out _);
    }

    /// <summary>Local prefix eviction without re-broadcasting. Called from pub/sub subscriber.</summary>
    internal int RemoveLocalByPrefix(string prefix)
    {
        if (string.IsNullOrEmpty(prefix)) return 0;
        var matching = _tracked.Keys.Where(k => k.StartsWith(prefix, StringComparison.Ordinal)).ToArray();
        foreach (var key in matching)
            RemoveLocal(key);
        return matching.Length;
    }

    /// <summary>Local L1 flush without re-broadcasting. Called from pub/sub subscriber.</summary>
    internal void FlushLocalL1()
    {
        foreach (var key in _tracked.Keys.ToArray())
            _memory.Remove(key);
        _tracked.Clear();
    }

    private void SetL1<T>(string key, T value, TimeSpan ttl) where T : class
    {
        var l1Ttl = ttl < _l1Cap ? ttl : _l1Cap;
        _memory.Set(key, value, new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = l1Ttl });
    }

    private async Task SetL2Async<T>(string key, T value, TimeSpan ttl, CancellationToken ct) where T : class
    {
        try
        {
            var payload = SerializePayload(value);
            await _distributed.SetAsync(
                key,
                payload,
                new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = ttl },
                ct);
            _logger.LogDebug("L2 cache set: {Key}", key);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "L2 cache write failed for {Key}", key);
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
            _logger.LogWarning(ex, "Cache payload deserialize failed");
            return null;
        }
    }

    private void Track(string key, TimeSpan ttl)
    {
        var now = DateTimeOffset.UtcNow;
        var softWindow = TimeSpan.FromTicks((long)(ttl.Ticks * _swrRatio));
        _tracked[key] = new CacheEntryMeta(now + softWindow, now + ttl);
    }

    /// <summary>
    /// If the entry is past its soft TTL but still within the hard TTL, refresh it on a
    /// background task so the next reader gets a fresh value with no added latency.
    /// </summary>
    private void MaybeBackgroundRefresh<T>(string key, Func<Task<T?>> factory, TimeSpan ttl) where T : class
    {
        if (!_swrEnabled) return;
        if (!_tracked.TryGetValue(key, out var meta)) return;

        var now = DateTimeOffset.UtcNow;
        if (now < meta.SoftExpiry) return;

        // Only one background refresh per key
        if (!_refreshing.TryAdd(key, 1)) return;

        _ = Task.Run(async () =>
        {
            try
            {
                var fresh = await factory();
                if (fresh is null) return;

                await SetL2Async(key, fresh, ttl, CancellationToken.None);
                SetL1(key, fresh, ttl);
                Track(key, ttl);
                _logger.LogDebug("SWR background refresh complete: {Key}", key);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "SWR background refresh failed for {Key} (serving stale)", key);
            }
            finally
            {
                _refreshing.TryRemove(key, out _);
            }
        });
    }

    private readonly record struct CacheEntryMeta(DateTimeOffset SoftExpiry, DateTimeOffset HardExpiry);
}
