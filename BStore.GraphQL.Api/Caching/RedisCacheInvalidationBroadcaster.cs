using System.Text.Json;
using BStore.GraphQL.Api.Configuration;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace BStore.GraphQL.Api.Caching;

/// <summary>
/// Wire format used by <see cref="RedisCacheInvalidationBroadcaster"/> and
/// <see cref="RedisCacheInvalidationSubscriber"/>. Single shared schema keeps the channel
/// payloads parseable by every instance (and any future debugging consumer).
/// </summary>
public sealed record CacheInvalidationMessage(
    string Kind,           // "key" | "prefix" | "flush"
    string OriginInstance, // identifies the publisher so it can ignore its own echo
    string? Key = null,
    string? Prefix = null,
    int? Layers = null);   // matches CacheLayer flag values

/// <summary>
/// Publishes invalidation/flush messages on a Redis Pub/Sub channel so peer API instances
/// can drop their local L1 entries even though L2 (Redis) is shared.
/// </summary>
public sealed class RedisCacheInvalidationBroadcaster(
    IConnectionMultiplexer redis,
    IOptions<CachingOptions> options,
    ILogger<RedisCacheInvalidationBroadcaster> logger) : ICacheInvalidationBroadcaster
{
    private readonly string _channel       = options.Value.PubSubChannel;
    private readonly string _flushChannel  = options.Value.PubSubFlushChannel;

    /// <summary>Per-process identifier — stamped on every message so the subscriber can ignore self-echo.</summary>
    public static readonly string InstanceId = $"{Environment.MachineName}:{Environment.ProcessId}:{Guid.NewGuid():N}";

    public async Task PublishKeyInvalidationAsync(string key, CancellationToken ct = default)
    {
        var msg = new CacheInvalidationMessage("key", InstanceId, Key: key);
        await PublishAsync(_channel, msg);
    }

    public async Task PublishPrefixInvalidationAsync(string prefix, CancellationToken ct = default)
    {
        var msg = new CacheInvalidationMessage("prefix", InstanceId, Prefix: prefix);
        await PublishAsync(_channel, msg);
    }

    public async Task PublishFlushAsync(CacheLayer layers, CancellationToken ct = default)
    {
        var msg = new CacheInvalidationMessage("flush", InstanceId, Layers: (int)layers);
        await PublishAsync(_flushChannel, msg);
    }

    private async Task PublishAsync(string channel, CacheInvalidationMessage msg)
    {
        try
        {
            var payload = JsonSerializer.SerializeToUtf8Bytes(msg);
            var subscriber = redis.GetSubscriber();
            await subscriber.PublishAsync(RedisChannel.Literal(channel), payload);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Cache invalidation broadcast failed | Channel={Channel} Kind={Kind}", channel, msg.Kind);
        }
    }
}
