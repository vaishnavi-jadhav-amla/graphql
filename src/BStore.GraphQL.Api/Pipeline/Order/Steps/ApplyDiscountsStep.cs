namespace BStore.GraphQL.Api.Pipeline.Order.Steps;

/// <summary>
/// Step 3: Applies coupon codes and promotion rules.
/// Non-critical — if discount service is unavailable, order proceeds without discounts.
/// </summary>
public sealed class ApplyDiscountsStep : IPipelineStep<OrderPipelineContext>
{
    private readonly ILogger<ApplyDiscountsStep> _logger;

    public ApplyDiscountsStep(ILogger<ApplyDiscountsStep> logger) => _logger = logger;

    public string Name => "ApplyDiscounts";
    public int Order => 300;
    public bool IsCritical => false;

    public Task<OrderPipelineContext> ExecuteAsync(OrderPipelineContext context, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(context.Input.CouponCode))
        {
            _logger.LogDebug("No coupon code; skipping discount step");
            return Task.FromResult(context);
        }

        // Discount logic would validate and apply the coupon here.
        _logger.LogDebug("Applying coupon {Coupon} to cart {CartId}",
            context.Input.CouponCode, context.Input.CartId);
        return Task.FromResult(context);
    }
}
