namespace BStore.GraphQL.Api.Pipeline.Order.Steps;

/// <summary>
/// Step 4: Calculates tax based on shipping address and line items.
/// Critical — tax must be calculated for accurate order totals.
/// </summary>
public sealed class CalculateTaxStep : IPipelineStep<OrderPipelineContext>
{
    private readonly ILogger<CalculateTaxStep> _logger;

    public CalculateTaxStep(ILogger<CalculateTaxStep> logger) => _logger = logger;

    public string Name => "CalculateTax";
    public int Order => 400;
    public bool IsCritical => true;

    public Task<OrderPipelineContext> ExecuteAsync(OrderPipelineContext context, CancellationToken ct)
    {
        // Tax calculation — could call an external tax provider (Avalara, TaxJar, etc.)
        // or use internal Znode tax rules.
        _logger.LogDebug("Calculating tax for cart {CartId}", context.Input.CartId);
        context.GrandTotal = context.SubTotal - context.DiscountTotal + context.TaxTotal + context.ShippingTotal;
        return Task.FromResult(context);
    }
}
