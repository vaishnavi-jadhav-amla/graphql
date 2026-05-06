namespace BStore.GraphQL.Api.Interceptors.Samples;

/// <summary>
/// Sample after-action: triggers async ERP synchronization after a successful order placement.
/// Runs via the <see cref="BackgroundActionChannel"/> — never blocks the GraphQL response.
/// </summary>
public sealed class ErpSyncAfterOrderAction : IAfterAction
{
    private readonly ILogger<ErpSyncAfterOrderAction> _logger;

    public ErpSyncAfterOrderAction(ILogger<ErpSyncAfterOrderAction> logger) => _logger = logger;

    public IReadOnlySet<string> Operations { get; } = new HashSet<string>
    {
        "placeOrder", "bStoreCreate"
    };

    public int Order => 200;

    public Task ExecuteAsync(InterceptorContext context, object? result, CancellationToken ct)
    {
        _logger.LogInformation(
            "[ErpSync] Triggering ERP synchronization for {Operation}, CorrelationId={CorrelationId}",
            context.OperationName,
            context.DebugContext.CorrelationId);

        // In a real implementation, this would:
        // 1. Extract the order/entity id from the result
        // 2. Call the ERP API (SAP, Oracle, NetSuite, etc.)
        // 3. Handle retries and dead-letter queues for failed syncs
        // Since this runs in BackgroundActionChannel, it doesn't block the response.

        return Task.CompletedTask;
    }
}
