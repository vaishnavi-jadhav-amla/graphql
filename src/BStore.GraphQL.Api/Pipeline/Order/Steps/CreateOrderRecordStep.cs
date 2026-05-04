namespace BStore.GraphQL.Api.Pipeline.Order.Steps;

/// <summary>
/// Step 5: Writes the order record to the database.
/// Critical — the order must be persisted.
/// </summary>
public sealed class CreateOrderRecordStep : IPipelineStep<OrderPipelineContext>
{
    private readonly ILogger<CreateOrderRecordStep> _logger;

    public CreateOrderRecordStep(ILogger<CreateOrderRecordStep> logger) => _logger = logger;

    public string Name => "CreateOrderRecord";
    public int Order => 500;
    public bool IsCritical => true;

    public Task<OrderPipelineContext> ExecuteAsync(OrderPipelineContext context, CancellationToken ct)
    {
        // Database insert would happen here via EF Core or stored procedure.
        _logger.LogDebug("Creating order record for cart {CartId}, total {Total}",
            context.Input.CartId, context.GrandTotal);
        return Task.FromResult(context);
    }
}
