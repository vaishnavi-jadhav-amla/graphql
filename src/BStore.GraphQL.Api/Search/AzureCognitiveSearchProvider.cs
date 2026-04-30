using BStore.GraphQL.Api.Configuration;
using BStore.GraphQL.Api.Diagnostics;
using Microsoft.Extensions.Options;

namespace BStore.GraphQL.Api.Search;

/// <summary>
/// ADR-011 production search backend. Wire the Azure.Search.Documents SDK in
/// <see cref="SearchAsync{T}"/>; this stub is intentionally vendor-free so the dependency
/// can be added without churning the registration surface.
/// </summary>
public sealed class AzureCognitiveSearchProvider(
    IOptions<SearchOptions> options,
    IRequestDebugContext debug,
    IProviderHealthTracker providerHealth,
    ILogger<AzureCognitiveSearchProvider> logger) : ISearchProvider
{
    private readonly SearchOptions _opts = options.Value;
    public string ProviderName => "AzureCognitiveSearch";

    public Task<SearchHits<T>> SearchAsync<T>(SearchQuery query, CancellationToken ct) where T : class
    {
        debug.RecordDataSource(DataSource.AzureCognitiveSearch);
        debug.Note("search.azure", $"index={query.Index} term={query.Text}");

        if (string.IsNullOrWhiteSpace(_opts.Endpoint) || string.IsNullOrWhiteSpace(_opts.ApiKey))
        {
            providerHealth.Record(DataSource.AzureCognitiveSearch, success: false, 0,
                "AzureCognitiveSearch endpoint/api key not configured");
            logger.LogWarning("AzureCognitiveSearch is not configured; returning empty hits");
            return Task.FromResult(new SearchHits<T>(Array.Empty<T>(), 0, ProviderName));
        }

        // Real implementation: var client = new SearchClient(new Uri(_opts.Endpoint), query.Index, new AzureKeyCredential(_opts.ApiKey));
        //                      var response = await client.SearchAsync<T>(query.Text, ...);
        // Left as a registration stub so the package reference can be added on a separate change.
        providerHealth.Record(DataSource.AzureCognitiveSearch, success: true, 0);
        return Task.FromResult(new SearchHits<T>(Array.Empty<T>(), 0, ProviderName));
    }
}

/// <summary>Bind from <c>"Search"</c> section.</summary>
public sealed class SearchOptions
{
    public const string Section = "Search";
    public string Provider { get; init; } = "Sql";
    public string Endpoint { get; init; } = "";
    public string ApiKey { get; init; } = "";
    public string ProductIndex { get; init; } = "products";
}
