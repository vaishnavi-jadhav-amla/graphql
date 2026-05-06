namespace BStore.GraphQL.Api.Configuration;

/// <summary>
/// Configuration for a single external data provider (Inventory, Pricing, Tax, Coupons).
/// </summary>
public sealed class ProviderSettings
{
    public bool Enabled { get; set; }
    public string Url { get; set; } = "";
    public string ApiKey { get; set; } = "";
    public int TimeoutMs { get; set; } = 3000;
    public int CacheTtlSeconds { get; set; } = 30;
    public bool FallbackToZnode { get; set; } = true;
    public Dictionary<string, string> ResponseMapping { get; set; } = new();
}

/// <summary>
/// Root options class for all external providers.
/// Bound to the <c>Providers</c> configuration section.
/// </summary>
public sealed class ProvidersOptions
{
    public const string Section = "Providers";
    public Dictionary<string, ProviderSettings> Providers { get; set; } = new();
}
