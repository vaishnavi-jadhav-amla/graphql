namespace BStore.GraphQL.Api.Configuration;

/// <summary>
/// Strongly-typed options for the BStore GraphQL service.
/// Bind from <c>appsettings.json</c> section <c>"GraphQL"</c> via
/// <c>builder.Services.Configure&lt;GraphQLOptions&gt;(configuration.GetSection(GraphQLOptions.Section))</c>.
/// </summary>
public sealed class GraphQLOptions
{
    public const string Section = "GraphQL";

    /// <summary>Default cache TTL (seconds) for read queries. Default: 60.</summary>
    public int DefaultCacheExpirySeconds { get; init; } = 60;

    /// <summary>Cache TTL (seconds) for B-store list queries (higher churn). Default: 30.</summary>
    public int ListCacheExpirySeconds { get; init; } = 30;

    /// <summary>Cache TTL (seconds) for domain and catalog look-ups. Default: 120.</summary>
    public int LookupCacheExpirySeconds { get; init; } = 120;

    /// <summary>
    /// Hard cap on connection / list page size (ADR-007). Per-page > 100 is rejected with INVALID_ARGUMENT.
    /// </summary>
    public int MaxPageSize { get; init; } = 100;

    /// <summary>Default Relay <c>first</c>/<c>last</c> when caller omits paging args. Default: 25 (ADR-010).</summary>
    public int DefaultRelayPageSize { get; init; } = 25;

    /// <summary>
    /// Maximum page index reachable via cursor-based pagination before we refuse and require finer filtering (ADR-010).
    /// At default <c>MaxPageSize</c> 100 and <c>MaxOffsetPages</c> 10 this is 1000 rows linearly walkable.
    /// </summary>
    public int MaxOffsetPages { get; init; } = 10;

    /// <summary>Allow GraphQL introspection (schema dump) in non-Development hosts. Default: <c>false</c> (ADR-007).</summary>
    public bool EnableIntrospectionInProd { get; init; } = false;

    /// <summary>
    /// When <c>true</c>, the Redis (distributed) cache is used; otherwise falls back to in-memory.
    /// Set via environment variable <c>GraphQL__UseRedis=true</c> in production.
    /// </summary>
    public bool UseRedis { get; init; } = false;

    /// <summary>Redis connection string. Used only when <see cref="UseRedis"/> is <c>true</c>.</summary>
    public string RedisConnectionString { get; init; } = string.Empty;

    /// <summary>Maximum field selection depth per request (Hot Chocolate <c>AddMaxExecutionDepthRule</c>). Default: 10 (ADR-007).</summary>
    public int MaxQueryDepth { get; init; } = 10;

    /// <summary>TTL (seconds) for inventory and pricing cache entries. ADR-016 requires <c>≤ 30</c>.</summary>
    public int InventoryPricingCacheSeconds { get; init; } = 15;

    /// <summary>SQL connection pool ceiling appended to connection strings if not present. ADR-005 requires 200.</summary>
    public int SqlMaxPoolSize { get; init; } = 200;

    /// <summary>Header that grants admin debug levels (ADR-026). Empty disables admin-token gating.</summary>
    public string AdminTokenHeader { get; init; } = "X-Bstore-Admin-Token";

    /// <summary>Bearer admin token; values must match this to expose Detailed debug extensions (ADR-026).</summary>
    public string AdminToken { get; init; } = string.Empty;

    /// <summary>Header read for the <see cref="Diagnostics.DebugLevel"/> per request. Default: <c>X-Debug-Level</c>.</summary>
    public string DebugLevelHeader { get; init; } = "X-Debug-Level";

    /// <summary>Log a warning when total request execution exceeds this many milliseconds. Default: 500.</summary>
    public int SlowQueryThresholdMs { get; init; } = 500;

    /// <summary>When <c>true</c> and the host is Development, a permissive CORS policy is applied to <c>/graphql</c>.</summary>
    public bool EnableDevCors { get; init; } = true;
}
