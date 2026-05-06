namespace BStore.GraphQL.Api.Diagnostics;

/// <summary>
/// Singleton counters and last-error tracking per backing provider (ADR-025).
/// </summary>
public interface IProviderHealthTracker
{
    /// <summary>Record a single provider call (success or failure).</summary>
    void Record(string provider, bool success, long elapsedMs, string? error = null);

    /// <summary>Snapshot for admin debug, /health/providers, and the <c>diagnose</c> query.</summary>
    IReadOnlyList<ProviderHealth> Snapshot();
}

public sealed record ProviderHealth(
    string Provider,
    long Calls,
    long Errors,
    long LastSuccessUnixMs,
    long LastErrorUnixMs,
    string? LastError,
    long P50Ms,
    long P95Ms);
