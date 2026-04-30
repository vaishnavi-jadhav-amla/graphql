using System.Text.Json;
using BStore.GraphQL.Api.Common;
using BStore.GraphQL.Api.Diagnostics;
using BStore.GraphQL.Api.GraphQL.Queries;
using HotChocolate;
using HotChocolate.Types;
using ErrorCodes = BStore.GraphQL.Api.Common.ErrorCodes;

namespace BStore.GraphQL.Api.GraphQL.Resolvers;

/// <summary>
/// Admin-only query that runs the empty-result diagnoser for an operation, plus returns
/// provider health and the current debug context (ADR-021).
/// </summary>
[ExtendObjectType(typeof(BStoreQuery))]
public sealed class DiagnoseQueryResolvers
{
    [GraphQLDescription("Admin-only: explain why an operation returned empty, plus provider health (ADR-021).")]
    public async Task<DiagnoseResult> Diagnose(
        string operation,
        string? argsJson,
        [Service] IRequestDebugContext debug,
        [Service] IEnumerable<IEmptyResultDiagnoser> diagnosers,
        [Service] IProviderHealthTracker provider,
        CancellationToken ct)
    {
        if (!debug.IsAdmin)
            throw new GraphQLException(ErrorBuilder.New()
                .SetMessage("diagnose() requires the admin debug token.")
                .SetCode(ErrorCodes.AdminRequired)
                .SetExtension("category", ErrorCategory.Authorization)
                .Build());

        var args = ParseArgs(argsJson);
        var diag = diagnosers.FirstOrDefault(d => string.Equals(d.Operation, operation, StringComparison.OrdinalIgnoreCase));
        var reasons = diag is null
            ? Array.Empty<EmptyResultReason>()
            : await diag.DiagnoseAsync(args, ct);

        return new DiagnoseResult(
            operation,
            reasons.Select(r => new DiagnoseReason(r.Code, r.Message)).ToList(),
            provider.Snapshot()
                    .Select(p => new DiagnoseProvider(p.Provider, p.Calls, p.Errors, p.LastError, p.P50Ms, p.P95Ms))
                    .ToList(),
            debug.CorrelationId);
    }

    private static IReadOnlyDictionary<string, object?> ParseArgs(string? argsJson)
    {
        if (string.IsNullOrWhiteSpace(argsJson))
            return new Dictionary<string, object?>();
        try
        {
            using var doc = JsonDocument.Parse(argsJson);
            var dict = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            foreach (var prop in doc.RootElement.EnumerateObject())
                dict[prop.Name] = prop.Value.ValueKind switch
                {
                    JsonValueKind.Number => prop.Value.TryGetInt32(out var i) ? i : prop.Value.GetDouble(),
                    JsonValueKind.String => prop.Value.GetString(),
                    JsonValueKind.True   => true,
                    JsonValueKind.False  => false,
                    _                    => prop.Value.ToString()
                };
            return dict;
        }
        catch
        {
            return new Dictionary<string, object?>();
        }
    }
}

public sealed record DiagnoseResult(
    string Operation,
    IReadOnlyList<DiagnoseReason> Reasons,
    IReadOnlyList<DiagnoseProvider> Providers,
    string CorrelationId);

public sealed record DiagnoseReason(string Code, string Message);

public sealed record DiagnoseProvider(string Provider, long Calls, long Errors, string? LastError, long P50Ms, long P95Ms);
