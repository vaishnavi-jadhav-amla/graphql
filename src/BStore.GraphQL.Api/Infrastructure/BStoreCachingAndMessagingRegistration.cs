using BStore.GraphQL.Api.Caching;
using BStore.GraphQL.Api.Configuration;
using BStore.GraphQL.Api.Messaging;
using StackExchange.Redis;

namespace BStore.GraphQL.Api.Infrastructure;

/// <summary>
/// Registers L1/L2 caching (memory + Redis), Redis Pub/Sub L1 invalidation,
/// scoped cache flushing, and optional RabbitMQ publish/subscribe.
/// </summary>
public static class BStoreCachingAndMessagingRegistration
{
    public static WebApplicationBuilder AddBStoreCachingAndMessaging(this WebApplicationBuilder builder)
    {
        builder.Services.Configure<CachingOptions>(
            builder.Configuration.GetSection(CachingOptions.Section));
        builder.Services.Configure<RabbitMqOptions>(
            builder.Configuration.GetSection(RabbitMqOptions.Section));

        var cacheOpts = builder.Configuration.GetSection(CachingOptions.Section).Get<CachingOptions>() ?? new CachingOptions();
        var gqlOpts   = builder.Configuration.GetSection(GraphQLOptions.Section).Get<GraphQLOptions>() ?? new GraphQLOptions();

        var redisConn = !string.IsNullOrWhiteSpace(cacheOpts.RedisConnectionString)
            ? cacheOpts.RedisConnectionString
            : gqlOpts.RedisConnectionString;

        var useRedis = (cacheOpts.UseRedis || gqlOpts.UseRedis)
                       && !string.IsNullOrWhiteSpace(redisConn);

        builder.Services.AddMemoryCache();

        if (useRedis)
        {
            builder.Services.AddStackExchangeRedisCache(o =>
            {
                o.Configuration  = redisConn;
                o.InstanceName   = string.IsNullOrWhiteSpace(cacheOpts.RedisInstanceName)
                    ? "BStoreGraphQL:"
                    : cacheOpts.RedisInstanceName;
            });

            // Singleton ConnectionMultiplexer is reused for Pub/Sub broadcasts. Microsoft's
            // distributed cache wrapper opens its own multiplexer internally; we open a second
            // (named) one here for low-overhead pub/sub use.
            builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
                ConnectionMultiplexer.Connect(redisConn!));
        }
        else
        {
            builder.Services.AddDistributedMemoryCache();
        }

        // Pub/Sub broadcaster — Redis impl when configured, otherwise a no-op.
        if (useRedis && cacheOpts.EnablePubSubInvalidation)
        {
            builder.Services.AddSingleton<ICacheInvalidationBroadcaster, RedisCacheInvalidationBroadcaster>();
            builder.Services.AddHostedService<RedisCacheInvalidationSubscriber>();
        }
        else
        {
            builder.Services.AddSingleton<ICacheInvalidationBroadcaster, NullCacheInvalidationBroadcaster>();
        }

        builder.Services.AddSingleton<ICacheService, LayeredCacheService>();
        builder.Services.AddSingleton<ICacheFlushService, CacheFlushService>();

        var rmq = builder.Configuration.GetSection(RabbitMqOptions.Section).Get<RabbitMqOptions>() ?? new RabbitMqOptions();
        if (rmq.Enabled)
        {
            builder.Services.AddSingleton<RabbitMqEventPublisher>();
            builder.Services.AddSingleton<IEventPublisher>(sp => sp.GetRequiredService<RabbitMqEventPublisher>());
            builder.Services.AddHostedService<RabbitMqPublisherShutdownHostedService>();
            if (rmq.ConsumerEnabled)
                builder.Services.AddHostedService<RabbitMqEventConsumerHostedService>();
        }
        else
        {
            builder.Services.AddSingleton<IEventPublisher, NullEventPublisher>();
        }

        return builder;
    }
}
