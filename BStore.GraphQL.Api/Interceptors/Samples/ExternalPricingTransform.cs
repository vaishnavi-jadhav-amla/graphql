using BStore.GraphQL.Api.Providers;

namespace BStore.GraphQL.Api.Interceptors.Samples;

/// <summary>
/// Sample transform: enriches product results with external pricing data
/// when the Pricing provider is enabled. Falls back to original result if unavailable.
/// </summary>
public sealed class ExternalPricingTransform : ITransformResult
{
    private readonly ILogger<ExternalPricingTransform> _logger;

    public ExternalPricingTransform(ILogger<ExternalPricingTransform> logger) => _logger = logger;

    public IReadOnlySet<string> Operations { get; } = new HashSet<string>
    {
        "product", "productList"
    };

    public int Order => 100;

    public Task<object?> TransformAsync(InterceptorContext context, object? result, CancellationToken ct)
    {
        var registry = context.Services.GetService<IProviderRegistry>();
        if (registry is null || !registry.IsEnabled("Pricing"))
            return Task.FromResult(result);

        try
        {
            _logger.LogDebug("Enriching {Operation} result with external pricing", context.OperationName);
            // Pricing enrichment would modify result properties here.
            // This is a hook point — concrete implementation depends on the provider response shape.
            return Task.FromResult(result);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "External pricing transform failed for {Operation}; returning original result",
                context.OperationName);
            return Task.FromResult(result);
        }
    }
}
