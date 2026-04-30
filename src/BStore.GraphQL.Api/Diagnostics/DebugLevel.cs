namespace BStore.GraphQL.Api.Diagnostics;

/// <summary>
/// Per-request verbosity level (ADR-022/023/026). Higher levels are admin-gated.
/// </summary>
public enum DebugLevel
{
    /// <summary>No diagnostic extensions emitted.</summary>
    None = 0,

    /// <summary>Correlation id, operation name, total duration. Available to any caller.</summary>
    Basic = 1,

    /// <summary>Per-stage timings, datasource attribution, pipeline trace, provider health. Admin only.</summary>
    Detailed = 2
}
