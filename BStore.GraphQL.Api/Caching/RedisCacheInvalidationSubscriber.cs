using System.Text.Json;
using BStore.GraphQL.Api.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace BStore.GraphQL.Api.Caching;

/// <summary>
/// Subscribes to the Redis Pub/Sub channels used by <see cref="RedisCacheInvalidationBroadcaster"/>
/// and applies the requested L1 evictions on this instance. Self-echoes are ignored via the
/// <c>OriginInstance</c> field on the message.
/// </summary>
public sealed class RedisCacheInvalidationSubscriber(
    IConnectionMultiplexer redis,
    IServiceProvider services,
    IOptions<CachingOptions> options,
    ILogger<RedisCacheInvalidationSubscriber> logger) : IHostedService
{
    private readonly string _channel      = options.Value.PubSubChannel;
    private readonly string _flushChannel = options.Value.PubSubFlushChannel;
    private ChannelMessageQueue? _keyQueue;
    private ChannelMessageQueue? _flushQueue;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var sub = redis.GetSubscriber();
        _keyQueue   = await sub.SubscribeAsync(RedisChannel.Literal(_channel));
        _flushQueue = await sub.SubscribeAsync(RedisChannel.Literal(_flushChannel));

        _keyQueue.OnMessage(msg => Handle(msg, isFlush: false));
        _flushQueue.OnMessage(msg => Handle(msg, isFlush: true));

        logger.LogInformation(
            "Redis Pub/Sub L1 invalidation subscriber started | KeyChannel={KeyChannel} FlushChannel={FlushChannel} InstanceId={InstanceId}",
            _channel, _flushChannel, RedisCacheInvalidationBroadcaster.InstanceId);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_keyQueue is not null)   await _keyQueue.UnsubscribeAsync();
        if (_flushQueue is not null) await _flushQueue.UnsubscribeAsync();
    }

    private void Handle(ChannelMessage msg, bool isFlush)
    {
        try
        {
            var payload = (byte[]?)msg.Message;
            if (payload is null || payload.Length == 0) return;

            var parsed = JsonSerializer.Deserialize<CacheInvalidationMessage>(payload);
            if (parsed is null) return;

            // Ignore the message we just published ourselves.
            if (parsed.OriginInstance == RedisCacheInvalidationBroadcaster.InstanceId)
                return;

            // The L1 cache is process-local; we resolve it lazily so the subscriber survives
            // a restart of the cache singleton (and avoids a circular DI dep).
            var cache = services.GetService(typeof(ICacheService)) as LayeredCacheService;
            if (cache is null)
            {
                logger.LogDebug("Pub/Sub message dropped: LayeredCacheService not registered");
                return;
            }

            switch (parsed.Kind)
            {
                case "key" when !string.IsNullOrEmpty(parsed.Key):
                    cache.RemoveLocal(parsed.Key);
                    logger.LogDebug("Pub/Sub L1 evict (key): {Key}", parsed.Key);
                    break;

                case "prefix" when !string.IsNullOrEmpty(parsed.Prefix):
                    var n = cache.RemoveLocalByPrefix(parsed.Prefix);
                    logger.LogDebug("Pub/Sub L1 evict (prefix): {Prefix} → {Count} keys", parsed.Prefix, n);
                    break;

                case "flush":
                    var layers = (CacheLayer)(parsed.Layers ?? (int)CacheLayer.L1);
                    if (layers.HasFlag(CacheLayer.L1))
                    {
                        cache.FlushLocalL1();
                        logger.LogInformation("Pub/Sub L1 flush applied (peer requested layers={Layers})", layers);
                    }
                    break;

                default:
                    logger.LogDebug("Pub/Sub message ignored (unknown kind): {Kind}", parsed.Kind);
                    break;
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to apply Pub/Sub cache invalidation message");
        }
    }
}
