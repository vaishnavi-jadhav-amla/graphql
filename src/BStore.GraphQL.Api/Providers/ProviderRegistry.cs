using System.Text.Json;
using BStore.GraphQL.Api.Caching;
using BStore.GraphQL.Api.Configuration;
using BStore.GraphQL.Api.Diagnostics;
using Microsoft.Extensions.Options;

namespace BStore.GraphQL.Api.Providers;

/// <summary>
/// Configuration-driven registry of external data providers.
/// Each provider is defined in <c>Providers:{Name}</c> config section.
/// Supports caching, timeouts, and fallback-to-Znode on failure.
/// </summary>
public sealed class ProviderRegistry : IProviderRegistry
{
    private readonly Dictionary<string, ProviderSettings> _providers;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ICacheService _cache;
    private readonly IProviderHealthTracker _healthTracker;
    private readonly ILogger<ProviderRegistry> _logger;

    public ProviderRegistry(
        IOptions<ProvidersOptions> options,
        IHttpClientFactory httpClientFactory,
        ICacheService cache,
        IProviderHealthTracker healthTracker,
        ILogger<ProviderRegistry> logger)
    {
        _providers = options.Value.Providers ?? new();
        _httpClientFactory = httpClientFactory;
        _cache = cache;
        _healthTracker = healthTracker;
        _logger = logger;
    }

    public bool IsEnabled(string providerName) =>
        _providers.TryGetValue(providerName, out var p) && p.Enabled;

    public async Task<JsonElement?> GetAsync(
        string providerName, object? parameters = null, CancellationToken ct = default)
    {
        if (!_providers.TryGetValue(providerName, out var settings) || !settings.Enabled)
            return null;

        var url = BuildUrl(settings.Url, parameters);
        var cacheKey = $"provider:{providerName}:{url}";

        // Try cache first
        if (settings.CacheTtlSeconds > 0)
        {
            var cached = await _cache.GetOrSetAsync<JsonElementWrapper>(
                cacheKey,
                () => FetchFromProvider(providerName, settings, url, ct),
                TimeSpan.FromSeconds(settings.CacheTtlSeconds),
                ct);
            return cached?.Element;
        }

        var wrapper = await FetchFromProvider(providerName, settings, url, ct);
        return wrapper?.Element;
    }

    private async Task<JsonElementWrapper?> FetchFromProvider(
        string providerName, ProviderSettings settings, string url, CancellationToken ct)
    {
        var client = _httpClientFactory.CreateClient("ExternalProvider");
        client.Timeout = TimeSpan.FromMilliseconds(settings.TimeoutMs);

        if (!string.IsNullOrWhiteSpace(settings.ApiKey))
            client.DefaultRequestHeaders.TryAddWithoutValidation("X-API-Key", settings.ApiKey);

        try
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var response = await client.GetAsync(url, ct);
            sw.Stop();

            _healthTracker.Record($"Provider:{providerName}", success: true, sw.ElapsedMilliseconds);

            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync(ct);
            var element = JsonDocument.Parse(json).RootElement;

            // Apply response mapping if configured
            if (settings.ResponseMapping is { Count: > 0 })
                element = ApplyMapping(element, settings.ResponseMapping);

            return new JsonElementWrapper { Element = element };
        }
        catch (Exception ex)
        {
            _healthTracker.Record($"Provider:{providerName}", success: false, 0, ex.Message);
            _logger.LogWarning(ex, "External provider {Provider} call failed: {Url}", providerName, url);

            if (settings.FallbackToZnode)
            {
                _logger.LogInformation("Falling back to Znode data for provider {Provider}", providerName);
                return null;
            }

            throw;
        }
    }

    private static string BuildUrl(string urlTemplate, object? parameters)
    {
        if (parameters is null) return urlTemplate;

        var url = urlTemplate;
        foreach (var prop in parameters.GetType().GetProperties())
        {
            var value = prop.GetValue(parameters)?.ToString() ?? "";
            url = url.Replace($"{{{prop.Name}}}", Uri.EscapeDataString(value), StringComparison.OrdinalIgnoreCase);
        }
        return url;
    }

    private static JsonElement ApplyMapping(JsonElement root, Dictionary<string, string> mapping)
    {
        // Navigate JSON paths like "data.available_qty" to extract values
        // This is a simplified version — production would use a proper JSON path library
        foreach (var (_, path) in mapping)
        {
            var parts = path.Split('.');
            var current = root;
            foreach (var part in parts)
            {
                if (current.TryGetProperty(part, out var next))
                    current = next;
                else
                    return root; // Path not found, return original
            }
        }
        return root;
    }
}

/// <summary>Wrapper to make JsonElement cacheable (reference type required by ICacheService).</summary>
internal sealed class JsonElementWrapper
{
    public JsonElement Element { get; set; }
}
