namespace BStore.GraphQL.Api.Pipeline;

/// <summary>
/// A single step in a processing pipeline. Steps execute in <see cref="Order"/> sequence.
/// Each step receives and returns the context, allowing it to enrich or short-circuit the pipeline.
/// </summary>
/// <typeparam name="TContext">The pipeline context type (e.g. OrderPipelineContext).</typeparam>
public interface IPipelineStep<TContext> where TContext : class
{
    /// <summary>Step name for logging and diagnostics.</summary>
    string Name { get; }

    /// <summary>Execution order — lower runs first.</summary>
    int Order { get; }

    /// <summary>If true, pipeline aborts on failure. If false, failure is logged and pipeline continues.</summary>
    bool IsCritical { get; }

    /// <summary>Execute the step. Modify context as needed and return it.</summary>
    Task<TContext> ExecuteAsync(TContext context, CancellationToken ct);
}
