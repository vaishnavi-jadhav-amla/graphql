namespace BStore.GraphQL.Api.Interceptors.Samples;

/// <summary>
/// Sample after-action: logs security-sensitive mutations for audit trail.
/// Tracks who performed what mutation, when, and from which correlation id.
/// </summary>
public sealed class SecurityAuditInterceptor : IAfterAction
{
    private readonly ILogger<SecurityAuditInterceptor> _logger;

    public SecurityAuditInterceptor(ILogger<SecurityAuditInterceptor> logger) => _logger = logger;

    public IReadOnlySet<string> Operations { get; } = new HashSet<string>
    {
        "bStoreCreate", "bStoreSetActivation", "bStoreUpdate",
        "bStoreUserRoleAccessSave", "bStoreUserAccessToggle",
        "userUpdate", "userToggleActive"
    };

    public int Order => 100;

    public Task ExecuteAsync(InterceptorContext context, object? result, CancellationToken ct)
    {
        _logger.LogInformation(
            "[SecurityAudit] Mutation={Operation} UserId={UserId} CorrelationId={CorrelationId}",
            context.OperationName,
            context.DebugContext.UserId,
            context.DebugContext.CorrelationId);
        return Task.CompletedTask;
    }
}
