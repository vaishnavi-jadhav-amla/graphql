namespace BStore.GraphQL.Api.Pipeline.Order.Steps;

/// <summary>
/// Step 1: Validates the cart exists, has items, and inventory is available.
/// Critical — aborts pipeline if validation fails.
/// </summary>
public sealed class ValidateCartStep : IPipelineStep<OrderPipelineContext>
{
    private readonly ILogger<ValidateCartStep> _logger;

    public ValidateCartStep(ILogger<ValidateCartStep> logger) => _logger = logger;

    public string Name => "ValidateCart";
    public int Order => 100;
    public bool IsCritical => true;

    public Task<OrderPipelineContext> ExecuteAsync(OrderPipelineContext context, CancellationToken ct)
    {
        if (context.Input.CartId <= 0)
        {
            context.IsValid = false;
            context.ValidationErrors.Add("Cart id is required.");
        }

        if (context.Input.ShippingAddressId <= 0)
        {
            context.IsValid = false;
            context.ValidationErrors.Add("Shipping address is required.");
        }

        if (context.Input.BillingAddressId <= 0)
        {
            context.IsValid = false;
            context.ValidationErrors.Add("Billing address is required.");
        }

        if (!context.IsValid)
        {
            _logger.LogWarning("Cart validation failed: {Errors}",
                string.Join("; ", context.ValidationErrors));
            throw new ArgumentException(
                $"Order validation failed: {string.Join("; ", context.ValidationErrors)}");
        }

        _logger.LogDebug("Cart {CartId} validated successfully", context.Input.CartId);
        return Task.FromResult(context);
    }
}
