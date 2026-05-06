namespace BStore.GraphQL.Api.Configuration;

/// <summary>
/// Tiered cache (L1 memory + L2 distributed) settings. Bind from <c>"Caching"</c>.
/// </summary>
public sealed class CachingOptions
{
    public const string Section = "Caching";

    /// <summary>Use StackExchange.Redis as L2. When false, L2 uses in-process distributed memory.</summary>
    public bool UseRedis { get; init; }

    /// <summary>StackExchange.Redis configuration string for L2.</summary>
    public string RedisConnectionString { get; init; } = string.Empty;

    /// <summary>Key prefix for all Redis entries (isolates environments). Default: <c>BStoreGraphQL:</c>.</summary>
    public string RedisInstanceName { get; init; } = "BStoreGraphQL:";

    /// <summary>Maximum time an entry may live in L1 (process memory). Default: 120 seconds.</summary>
    public int L1MaxEntrySeconds { get; init; } = 120;

    /// <summary>ADR-003: GZip compression on all L2 values. Default: <c>true</c>.</summary>
    public bool CompressL2Payloads { get; init; } = true;

    /// <summary>Below this byte threshold, GZip is skipped (overhead exceeds savings). Default: 256.</summary>
    public int CompressL2MinBytes { get; init; } = 256;

    // ── Stale-while-revalidate (ADR-029) ───────────────────────────────────

    /// <summary>
    /// When <c>true</c>, the cache returns a soft-expired entry immediately and refreshes it in the background
    /// (stale-while-revalidate). Reduces tail latency on hot keys after TTL crosses the soft window.
    /// </summary>
    public bool EnableStaleWhileRevalidate { get; init; } = true;

    /// <summary>
    /// Fraction of full TTL that marks the start of the "stale" window. Default <c>0.8</c> (80%).
    /// At <c>1.0</c> SWR is effectively disabled; at <c>0.5</c> half of every entry's life is stale.
    /// </summary>
    public double StaleAfterRatio { get; init; } = 0.8;

    // ── Cross-instance L1 invalidation via Redis Pub/Sub (ADR-030) ─────────

    /// <summary>
    /// When <c>true</c> and Redis is the L2 backing store, a Pub/Sub channel is used to evict L1 entries
    /// across all API instances when a key is removed locally. Required for multi-instance deployments.
    /// </summary>
    public bool EnablePubSubInvalidation { get; init; } = true;

    /// <summary>
    /// Redis Pub/Sub channel for L1 invalidation broadcasts. Default <c>BStoreGraphQL:invalidate</c>.
    /// All instances must use the same channel name.
    /// </summary>
    public string PubSubChannel { get; init; } = "BStoreGraphQL:invalidate";

    /// <summary>
    /// Channel name used for broadcasting full-flush requests across instances.
    /// Default <c>BStoreGraphQL:flush</c>.
    /// </summary>
    public string PubSubFlushChannel { get; init; } = "BStoreGraphQL:flush";
}
