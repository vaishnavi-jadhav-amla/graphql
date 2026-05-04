namespace BStore.GraphQL.Api.Pipeline;

/// <summary>
/// Configuration for pipeline behavior. Allows disabling specific steps via appsettings
/// without code changes.
/// Bound to the <c>Pipeline</c> configuration section.
/// </summary>
public sealed class PipelineSettings
{
    public const string Section = "Pipeline";

    /// <summary>
    /// List of step names to skip (e.g. ["SendConfirmation", "ApplyDiscounts"]).
    /// Steps are matched by <see cref="IPipelineStep{TContext}.Name"/>.
    /// </summary>
    public string[] DisabledSteps { get; set; } = [];

    /// <summary>
    /// If true, all non-critical steps are skipped. Useful for fast-path testing.
    /// </summary>
    public bool SkipNonCriticalSteps { get; set; }

    /// <summary>
    /// If true, pipeline logs detailed timing for each step.
    /// </summary>
    public bool EnableStepTiming { get; set; } = true;
}
