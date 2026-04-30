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
}
