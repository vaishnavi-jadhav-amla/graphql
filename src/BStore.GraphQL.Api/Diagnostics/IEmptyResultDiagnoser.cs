namespace BStore.GraphQL.Api.Diagnostics;

/// <summary>
/// Explains why a query returned no rows (ADR-020). Implementations probe the data layer
/// and infer the most-likely reason: missing portal, inactive portal, locale mismatch,
/// no published version, filter excluded everything, etc.
/// </summary>
public interface IEmptyResultDiagnoser
{
    /// <summary>Operation key the diagnoser supports (e.g. <c>productList</c>).</summary>
    string Operation { get; }

    /// <summary>
    /// Returns one or more <see cref="EmptyResultReason"/>s that explain why the operation produced no rows.
    /// </summary>
    Task<IReadOnlyList<EmptyResultReason>> DiagnoseAsync(
        IReadOnlyDictionary<string, object?> args,
        CancellationToken ct);
}
