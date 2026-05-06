using System.Text.Json;

namespace BStore.GraphQL.Api.Providers;

/// <summary>
/// Registry for external data providers (Inventory, Pricing, Tax, Coupons).
/// Configuration-driven — add new providers via appsettings.json without code changes.
/// </summary>
public interface IProviderRegistry
{
    /// <summary>Check if a named provider is enabled.</summary>
    bool IsEnabled(string providerName);

    /// <summary>
    /// Call an external provider and return the JSON response.
    /// Returns null if the provider is disabled or unavailable (with fallback logic).
    /// </summary>
    Task<JsonElement?> GetAsync(string providerName, object? parameters = null, CancellationToken ct = default);
}
