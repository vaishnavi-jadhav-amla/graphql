using System.Data;
using System.Reflection;
using BStore.GraphQL.Api.Diagnostics;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace BStore.GraphQL.Api.Bulk;

/// <summary>
/// ADR-017 default <see cref="IBulkWriter"/> using <see cref="SqlBulkCopy"/>. Maps public properties of
/// <typeparamref name="T"/> by name to columns of <paramref name="destinationTable"/>.
/// </summary>
public sealed class SqlBulkCopyWriter(
    IConfiguration config,
    IProviderHealthTracker providerHealth,
    IRequestDebugContext debug,
    ILogger<SqlBulkCopyWriter> log) : IBulkWriter
{
    public async Task BulkInsertAsync<T>(string destinationTable, IReadOnlyList<T> rows, CancellationToken ct) where T : class
    {
        if (rows.Count == 0) return;

        var conn = config.GetConnectionString("Znode_Entities")
                   ?? throw new InvalidOperationException("Znode_Entities connection string is required for bulk writes.");

        debug.RecordDataSource(DataSource.ZnodeEntities);
        debug.Note("bulk.sqlBulkCopy", $"table={destinationTable} rows={rows.Count}");

        var props = typeof(T)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead)
            .ToArray();

        using var table = new DataTable();
        foreach (var p in props)
            table.Columns.Add(p.Name, Nullable.GetUnderlyingType(p.PropertyType) ?? p.PropertyType);

        foreach (var row in rows)
        {
            var values = new object?[props.Length];
            for (int i = 0; i < props.Length; i++) values[i] = props[i].GetValue(row) ?? DBNull.Value;
            table.Rows.Add(values);
        }

        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            await using var sql = new SqlConnection(conn);
            await sql.OpenAsync(ct);
            using var bulk = new SqlBulkCopy(sql)
            {
                DestinationTableName = destinationTable,
                BatchSize            = 1000,
                BulkCopyTimeout      = 60
            };
            foreach (var p in props)
                bulk.ColumnMappings.Add(p.Name, p.Name);

            await bulk.WriteToServerAsync(table, ct);
            sw.Stop();
            providerHealth.Record(DataSource.ZnodeEntities, success: true, sw.ElapsedMilliseconds);
            log.LogInformation("Bulk insert | Table={Table} | Rows={Rows} | {Ms}ms", destinationTable, rows.Count, sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            sw.Stop();
            providerHealth.Record(DataSource.ZnodeEntities, success: false, sw.ElapsedMilliseconds, ex.Message);
            throw;
        }
    }
}
