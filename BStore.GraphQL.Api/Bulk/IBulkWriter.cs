namespace BStore.GraphQL.Api.Bulk;

/// <summary>
/// ADR-017: bulk writes must use <c>SqlBulkCopy</c>, never <c>SaveChanges</c> in a loop.
/// </summary>
public interface IBulkWriter
{
    /// <summary>Bulk-insert <paramref name="rows"/> into <paramref name="destinationTable"/>.</summary>
    Task BulkInsertAsync<T>(string destinationTable, IReadOnlyList<T> rows, CancellationToken ct) where T : class;
}
