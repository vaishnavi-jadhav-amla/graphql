namespace BStore.GraphQL.Api.Interceptors;

/// <summary>
/// Transforms a resolver's result before it is returned. Use for enrichment, redaction, pricing overrides.
/// </summary>
public interface ITransformResult
{
    /// <summary>Operation names this transform applies to. Empty = all.</summary>
    IReadOnlySet<string> Operations { get; }

    /// <summary>Execution order — lower runs first.</summary>
    int Order { get; }

    /// <summary>Transform the result. Return the modified (or original) result.</summary>
    Task<object?> TransformAsync(InterceptorContext context, object? result, CancellationToken ct);
}
