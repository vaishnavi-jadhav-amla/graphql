namespace BStore.GraphQL.Api.Interceptors;

/// <summary>
/// Runs before a resolver executes. Use for validation, auth checks, rate limiting, logging.
/// Multiple before-actions execute in <see cref="Order"/> sequence.
/// </summary>
public interface IBeforeAction
{
    /// <summary>Operation names this action applies to (e.g. "placeOrder", "addToCart"). Empty = all.</summary>
    IReadOnlySet<string> Operations { get; }

    /// <summary>Execution order — lower runs first.</summary>
    int Order { get; }

    /// <summary>Execute the before-action. Throw to abort the resolver.</summary>
    Task ExecuteAsync(InterceptorContext context, CancellationToken ct);
}
