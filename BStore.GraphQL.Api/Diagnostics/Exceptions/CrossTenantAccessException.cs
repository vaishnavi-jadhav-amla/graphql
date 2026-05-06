namespace BStore.GraphQL.Api.Diagnostics.Exceptions;

/// <summary>
/// Thrown when a request attempts to access data belonging to a different portal/tenant.
/// This is a security-critical exception — always logged at Warning level.
/// </summary>
public sealed class CrossTenantAccessException : Exception
{
    public int RequestedPortalId { get; }
    public int CallerPortalId { get; }

    public CrossTenantAccessException(int callerPortalId, int requestedPortalId)
        : base($"Cross-tenant access denied: caller portal {callerPortalId} cannot access portal {requestedPortalId}.")
    {
        CallerPortalId = callerPortalId;
        RequestedPortalId = requestedPortalId;
    }

    public CrossTenantAccessException(string message) : base(message) { }

    public CrossTenantAccessException(string message, Exception inner) : base(message, inner) { }
}
