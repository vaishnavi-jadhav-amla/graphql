namespace BStore.GraphQL.Api.Pipeline.Order.Steps;

/// <summary>
/// Step 2: Calculates line-item pricing, applies tier pricing rules.
/// Critical — pricing must succeed for the order to proceed.
/// </summary>
public sealed class CalculatePricingStep : IPipelineStep<OrderPipelineContext>
{
    private readonly ILogger<CalculatePricingStep> _logger;

    public CalculatePricingStep(ILogger<CalculatePricingStep> logger) => _logger = logger;

    public string Name => "CalculatePricing";
    public int Order => 200;
    public bool IsCritical => true;

    public Task<OrderPipelineContext> ExecuteAsync(OrderPipelineContext context, CancellationToken ct)
    {
        // Pricing calculation would load cart items and compute subtotal.
        // This is a hook point — concrete implementation queries the pricing service.
        _logger.LogDebug("Calculating pricing for cart {CartId}", context.Input.CartId);
        return Task.FromResult(context);
    }
}
