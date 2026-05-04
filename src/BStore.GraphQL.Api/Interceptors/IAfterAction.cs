namespace BStore.GraphQL.Api.Interceptors;

/// <summary>
/// Runs after a resolver completes (fire-and-forget). Use for ERP sync, audit logging, analytics.
/// Failures are logged but never propagated to the client.
/// </summary>
public interface IAfterAction
{
    /// <summary>Operation names this action applies to. Empty = all.</summary>
    IReadOnlySet<string> Operations { get; }

    /// <summary>Execution order — lower runs first.</summary>
    int Order { get; }

    /// <summary>Execute the after-action. Exceptions are caught and logged.</summary>
    Task ExecuteAsync(InterceptorContext context, object? result, CancellationToken ct);
}
