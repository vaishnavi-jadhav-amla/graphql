using System.Text.Json;
using BStore.GraphQL.Api.Auth;
using BStore.GraphQL.Api.Diagnostics;
using HotChocolate;
using HotChocolate.Authorization;
using HotChocolate.Types;

namespace BStore.GraphQL.Api.GraphQL.Schema.Admin;

/// <summary>
/// Admin-only diagnostics queries — debug context, provider health, empty result analysis.
/// </summary>
[ExtendObjectType(typeof(AdminQuery))]
public sealed class AdminDiagnoseQueryResolvers
{
    [Authorize(Policy = AuthConstants.PolicyAdminOnly)]
    [GraphQLDescription("Admin-only: explain why an operation returned empty, plus provider health.")]
    public async Task<DiagnoseResult> Diagnose(
        string operation,
        [Service] IEnumerable<IEmptyResultDiagnoser> diagnosers,
        [Service] IRequestDebugContext debug,
        [Service] IProviderHealthTracker health,
        CancellationToken ct)
    {
        var diagnoser = diagnosers.FirstOrDefault(d =>
            d.Operation.Equals(operation, StringComparison.OrdinalIgnoreCase));

        IReadOnlyList<EmptyResultReason>? reasons = null;
        if (diagnoser is not null)
            reasons = await diagnoser.DiagnoseAsync(new Dictionary<string, object?>(), ct);

        return new DiagnoseResult
        {
            Operation = operation,
            EmptyReasons = reasons?.Select(r => $"{r.Code}: {r.Message}").ToList() ?? [],
            ProviderHealth = JsonSerializer.Serialize(health.Snapshot()),
            DebugSnapshot = JsonSerializer.Serialize(debug.Snapshot())
        };
    }
}

public sealed class DiagnoseResult
{
    public string Operation { get; set; } = "";
    public List<string> EmptyReasons { get; set; } = [];
    public string? ProviderHealth { get; set; }
    public string? DebugSnapshot { get; set; }
}
