namespace BStore.GraphQL.Api.Auth;

/// <summary>
/// Per-request security context for admin operations.
/// Admins can access cross-portal data but are scoped to their portal membership.
/// </summary>
public interface IAdminAuthContext
{
    int UserId { get; }
    int PortalId { get; }
    bool IsAdmin { get; }

    /// <summary>
    /// Throws <see cref="Diagnostics.Exceptions.CrossTenantAccessException"/> if the admin
    /// is attempting to access a portal they are not a member of.
    /// </summary>
    void EnforcePortalAccess(int portalId);
}
