using System.Diagnostics;
using Microsoft.Extensions.Options;

namespace BStore.GraphQL.Api.Pipeline;

/// <summary>
/// Executes a sequence of <see cref="IPipelineStep{TContext}"/> in order.
/// Critical steps abort the pipeline on failure; non-critical steps log and continue.
/// Steps can be disabled via <see cref="PipelineSettings.DisabledSteps"/> configuration.
/// </summary>
public sealed class PipelineExecutor<TContext> where TContext : class
{
    private readonly IEnumerable<IPipelineStep<TContext>> _steps;
    private readonly PipelineSettings _settings;
    private readonly ILogger<PipelineExecutor<TContext>> _logger;

    public PipelineExecutor(
        IEnumerable<IPipelineStep<TContext>> steps,
        IOptions<PipelineSettings> settings,
        ILogger<PipelineExecutor<TContext>> logger)
    {
        _steps = steps;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<TContext> ExecuteAsync(TContext context, CancellationToken ct = default)
    {
        var disabledSet = new HashSet<string>(_settings.DisabledSteps, StringComparer.OrdinalIgnoreCase);

        foreach (var step in _steps.OrderBy(s => s.Order))
        {
            // Skip disabled steps
            if (disabledSet.Contains(step.Name))
            {
                _logger.LogInformation("Pipeline step {Step} skipped (disabled via config)", step.Name);
                continue;
            }

            // Skip non-critical steps if configured
            if (_settings.SkipNonCriticalSteps && !step.IsCritical)
            {
                _logger.LogInformation("Pipeline step {Step} skipped (non-critical, SkipNonCriticalSteps=true)", step.Name);
                continue;
            }

            var sw = _settings.EnableStepTiming ? Stopwatch.StartNew() : null;
            try
            {
                _logger.LogDebug("Pipeline step {Step} (order {Order}) starting", step.Name, step.Order);
                context = await step.ExecuteAsync(context, ct);
                sw?.Stop();

                if (sw is not null)
                    _logger.LogDebug("Pipeline step {Step} completed in {Elapsed}ms", step.Name, sw.ElapsedMilliseconds);
                else
                    _logger.LogDebug("Pipeline step {Step} completed", step.Name);
            }
            catch (Exception ex) when (!step.IsCritical)
            {
                sw?.Stop();
                _logger.LogWarning(ex,
                    "Non-critical pipeline step {Step} failed after {Elapsed}ms; continuing pipeline",
                    step.Name, sw?.ElapsedMilliseconds ?? 0);
            }
            catch (Exception ex)
            {
                sw?.Stop();
                _logger.LogError(ex,
                    "Critical pipeline step {Step} failed after {Elapsed}ms; aborting pipeline",
                    step.Name, sw?.ElapsedMilliseconds ?? 0);
                throw;
            }
        }

        return context;
    }
}
