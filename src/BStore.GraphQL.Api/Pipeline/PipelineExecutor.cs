namespace BStore.GraphQL.Api.Pipeline;

/// <summary>
/// Executes a sequence of <see cref="IPipelineStep{TContext}"/> in order.
/// Critical steps abort the pipeline on failure; non-critical steps log and continue.
/// </summary>
public sealed class PipelineExecutor<TContext> where TContext : class
{
    private readonly IEnumerable<IPipelineStep<TContext>> _steps;
    private readonly ILogger<PipelineExecutor<TContext>> _logger;

    public PipelineExecutor(
        IEnumerable<IPipelineStep<TContext>> steps,
        ILogger<PipelineExecutor<TContext>> logger)
    {
        _steps = steps;
        _logger = logger;
    }

    public async Task<TContext> ExecuteAsync(TContext context, CancellationToken ct = default)
    {
        foreach (var step in _steps.OrderBy(s => s.Order))
        {
            try
            {
                _logger.LogDebug("Pipeline step {Step} (order {Order}) starting", step.Name, step.Order);
                context = await step.ExecuteAsync(context, ct);
                _logger.LogDebug("Pipeline step {Step} completed", step.Name);
            }
            catch (Exception ex) when (!step.IsCritical)
            {
                _logger.LogWarning(ex,
                    "Non-critical pipeline step {Step} failed; continuing pipeline", step.Name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Critical pipeline step {Step} failed; aborting pipeline", step.Name);
                throw;
            }
        }

        return context;
    }
}
