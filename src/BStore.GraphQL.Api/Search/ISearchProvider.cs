namespace BStore.GraphQL.Api.Search;

/// <summary>
/// ADR-011: full-text search must go through this abstraction (Azure Cognitive Search in production).
/// SQL <c>LIKE</c> fallbacks are only allowed in <see cref="SqlLikeSearchProvider"/> for local dev.
/// </summary>
public interface ISearchProvider
{
    string ProviderName { get; }

    Task<SearchHits<T>> SearchAsync<T>(SearchQuery query, CancellationToken ct) where T : class;
}

public sealed record SearchQuery(
    string Index,
    string Text,
    int Top,
    int Skip,
    IReadOnlyDictionary<string, string>? Filters = null,
    string? OrderBy = null);

public sealed record SearchHits<T>(IReadOnlyList<T> Items, long Total, string ProviderName) where T : class;
