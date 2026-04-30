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

    public async Task<T?> GetOrSetAsync<T>(
        string key,
        Func<Task<T?>> factory,
        TimeSpan? expiry = null,
        CancellationToken ct = default) where T : class
    {
        if (memory.TryGetValue(key, out object? cached) && cached is T typed)
        {
            logger.LogDebug("L1 cache hit: {Key}", key);
            return typed;
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
        return value;
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
}
