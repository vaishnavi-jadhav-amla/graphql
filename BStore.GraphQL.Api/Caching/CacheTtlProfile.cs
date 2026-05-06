using BStore.GraphQL.Api.Configuration;
using Microsoft.Extensions.Options;

namespace BStore.GraphQL.Api.Caching;

/// <summary>
/// Centralised TTL policy. ADR-016 forbids inventory/pricing TTLs above 30s; this helper makes that
/// explicit so resolvers cannot accidentally cache stock or price for too long.
/// </summary>
public sealed class CacheTtlProfile(IOptions<GraphQLOptions> options)
{
    private readonly GraphQLOptions _opts = options.Value;

    public TimeSpan Default        => TimeSpan.FromSeconds(_opts.DefaultCacheExpirySeconds);
    public TimeSpan List           => TimeSpan.FromSeconds(_opts.ListCacheExpirySeconds);
    public TimeSpan Lookup         => TimeSpan.FromSeconds(_opts.LookupCacheExpirySeconds);

    /// <summary>Inventory and pricing — clamped to ADR-016's 30-second ceiling.</summary>
    public TimeSpan InventoryPricing
    {
        get
        {
            var s = Math.Min(_opts.InventoryPricingCacheSeconds, 30);
            return TimeSpan.FromSeconds(Math.Max(1, s));
        }
    }
}
