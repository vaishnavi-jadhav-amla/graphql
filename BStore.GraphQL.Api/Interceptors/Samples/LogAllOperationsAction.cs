namespace BStore.GraphQL.Api.Interceptors.Samples;

/// <summary>
/// Sample before-action: logs every GraphQL operation name with correlation id.
/// Applies to all operations (empty Operations set).
/// </summary>
public sealed class LogAllOperationsAction : IBeforeAction
{
    private readonly ILogger<LogAllOperationsAction> _logger;

    public LogAllOperationsAction(ILogger<LogAllOperationsAction> logger) => _logger = logger;

    public IReadOnlySet<string> Operations { get; } = new HashSet<string>();
    public int Order => 0;

    public Task ExecuteAsync(InterceptorContext context, CancellationToken ct)
    {
        _logger.LogInformation(
            "[Interceptor] Operation={Operation} Path={Path} CorrelationId={CorrelationId}",
            context.OperationName,
            context.FieldPath,
            context.DebugContext.CorrelationId);
        return Task.CompletedTask;
    }
}
