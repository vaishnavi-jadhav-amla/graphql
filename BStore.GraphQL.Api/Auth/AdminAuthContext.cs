using System.Security.Claims;
using BStore.GraphQL.Api.Diagnostics.Exceptions;

namespace BStore.GraphQL.Api.Auth;

/// <summary>
/// Scoped admin auth context. Enforces portal-level access for admin users.
/// </summary>
public sealed class AdminAuthContext : IAdminAuthContext
{
    public int UserId { get; }
    public int PortalId { get; }
    public bool IsAdmin { get; }

    public AdminAuthContext(IHttpContextAccessor httpContextAccessor)
    {
        var user = httpContextAccessor.HttpContext?.User;
        if (user?.Identity?.IsAuthenticated != true)
        {
            UserId = 0;
            PortalId = 0;
            IsAdmin = false;
            return;
        }

        UserId = int.TryParse(user.FindFirstValue(AuthConstants.ClaimUserId), out var uid) ? uid : 0;
        PortalId = int.TryParse(user.FindFirstValue(AuthConstants.ClaimPortalId), out var pid) ? pid : 0;
        IsAdmin = user.IsInRole(AuthConstants.RoleAdmin) || user.IsInRole(AuthConstants.RoleServerToServer);
    }

    public void EnforcePortalAccess(int portalId)
    {
        if (IsAdmin && PortalId != portalId && PortalId != 0)
            throw new CrossTenantAccessException(PortalId, portalId);
    }
}
