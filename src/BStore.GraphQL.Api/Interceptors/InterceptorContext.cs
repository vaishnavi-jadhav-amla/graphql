using BStore.GraphQL.Api.Diagnostics;

namespace BStore.GraphQL.Api.Interceptors;

/// <summary>
/// Context passed to all interceptor hooks. Provides access to the operation name,
/// resolver arguments, and the per-request debug context.
/// </summary>
public sealed class InterceptorContext
{
    /// <summary>The GraphQL field/operation name being executed.</summary>
    public required string OperationName { get; init; }

    /// <summary>The full path of the field (e.g. "mutation.placeOrder").</summary>
    public required string FieldPath { get; init; }

    /// <summary>Resolver arguments as a dictionary.</summary>
    public required IReadOnlyDictionary<string, object?> Arguments { get; init; }

    /// <summary>Per-request debug/diagnostic context.</summary>
    public required IRequestDebugContext DebugContext { get; init; }

    /// <summary>The scoped service provider for the current request.</summary>
    public required IServiceProvider Services { get; init; }
}
