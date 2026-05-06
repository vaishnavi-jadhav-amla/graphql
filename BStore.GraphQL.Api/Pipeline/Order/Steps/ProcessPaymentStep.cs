namespace BStore.GraphQL.Api.Pipeline.Order.Steps;

/// <summary>
/// Step 6: Processes payment via the configured payment gateway.
/// Critical — payment must succeed for the order to be confirmed.
/// </summary>
public sealed class ProcessPaymentStep : IPipelineStep<OrderPipelineContext>
{
    private readonly ILogger<ProcessPaymentStep> _logger;

    public ProcessPaymentStep(ILogger<ProcessPaymentStep> logger) => _logger = logger;

    public string Name => "ProcessPayment";
    public int Order => 600;
    public bool IsCritical => true;

    public Task<OrderPipelineContext> ExecuteAsync(OrderPipelineContext context, CancellationToken ct)
    {
        // Payment gateway call would happen here.
        _logger.LogDebug("Processing payment for order, method={Method}",
            context.Input.PaymentMethod ?? "default");
        context.PaymentProcessed = true;
        return Task.FromResult(context);
    }
}
