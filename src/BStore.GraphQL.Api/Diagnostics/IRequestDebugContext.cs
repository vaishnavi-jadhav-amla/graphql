namespace BStore.GraphQL.Api.Diagnostics;

/// <summary>
/// Per-request diagnostic surface (ADR-018/022/023/024/026).
/// Resolvers, services, EF interceptors, and the cache layer all push observations here;
/// the diagnostic listener emits them under the GraphQL response <c>extensions</c> key
/// when the caller is authorised for the requested <see cref="DebugLevel"/>.
/// </summary>
public interface IRequestDebugContext
{
    /// <summary>Stable id propagated as <c>X-Correlation-ID</c> across HTTP, AMQP, and logs.</summary>
    string CorrelationId { get; }

    /// <summary>Top-level GraphQL operation name (or <c>"anonymous"</c>).</summary>
    string OperationName { get; set; }

    /// <summary>Authenticated user id (claim sub/uid). 0 when anonymous.</summary>
    int UserId { get; set; }

    /// <summary>Logical client identity from <c>X-Client-Id</c> (storefront, admin, support).</summary>
    string? ClientId { get; set; }

    /// <summary>Effective debug verbosity after admin-token gating (ADR-026).</summary>
    DebugLevel Level { get; set; }

    /// <summary>True when the request was authenticated as an admin/support user (ADR-026).</summary>
    bool IsAdmin { get; set; }

    /// <summary>Mark a data source as having been touched (ADR-022).</summary>
    void RecordDataSource(string source);

    /// <summary>Begin a named pipeline stage; return value must be disposed to record duration (ADR-023/024).</summary>
    IDisposable Stage(string name);

    /// <summary>Append a free-form pipeline marker (decisions, branch picks). Always low-cost (ADR-024).</summary>
    void Note(string step, string? details = null);

    /// <summary>Diagnostic reasons the result was empty (ADR-020).</summary>
    void RecordEmptyResultReason(string code, string message);

    /// <summary>Snapshot for the diagnostic listener.</summary>
    RequestDiagnosticSnapshot Snapshot();
}
