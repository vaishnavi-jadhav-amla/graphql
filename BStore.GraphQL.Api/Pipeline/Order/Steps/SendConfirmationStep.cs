namespace BStore.GraphQL.Api.Pipeline.Order.Steps;

/// <summary>
/// Step 7: Sends order confirmation email.
/// Non-critical — email failure should not fail the order.
/// </summary>
public sealed class SendConfirmationStep : IPipelineStep<OrderPipelineContext>
{
    private readonly ILogger<SendConfirmationStep> _logger;

    public SendConfirmationStep(ILogger<SendConfirmationStep> logger) => _logger = logger;

    public string Name => "SendConfirmation";
    public int Order => 700;
    public bool IsCritical => false;

    public Task<OrderPipelineContext> ExecuteAsync(OrderPipelineContext context, CancellationToken ct)
    {
        _logger.LogDebug("Sending order confirmation for order {OrderId}", context.CreatedOrderId);
        // Email sending logic would go here.
        return Task.FromResult(context);
    }
}
