using System.Security.Claims;
using BStore.GraphQL.Api.Diagnostics.Exceptions;

namespace BStore.GraphQL.Api.Auth;

/// <summary>
/// Scoped auth context for B-store operations. Populated from the current user's JWT claims.
/// </summary>
public sealed class BStoreAuthContext : IBStoreAuthContext
{
    public int UserId { get; }
    public IReadOnlyList<int> AccessibleBStoreIds { get; }
    public bool IsAdmin { get; }

    public BStoreAuthContext(IHttpContextAccessor httpContextAccessor)
    {
        var user = httpContextAccessor.HttpContext?.User;
        if (user?.Identity?.IsAuthenticated != true)
        {
            UserId = 0;
            AccessibleBStoreIds = [];
            IsAdmin = false;
            return;
        }

        UserId = int.TryParse(user.FindFirstValue(AuthConstants.ClaimUserId), out var uid) ? uid : 0;
        IsAdmin = user.IsInRole(AuthConstants.RoleAdmin) || user.IsInRole(AuthConstants.RoleServerToServer);

        var bStoreIds = new List<int>();
        foreach (var claim in user.FindAll(AuthConstants.ClaimBStoreId))
        {
            if (int.TryParse(claim.Value, out var id))
                bStoreIds.Add(id);
        }
        AccessibleBStoreIds = bStoreIds;
    }

    public void EnforceBStoreAccess(int bStoreId)
    {
        if (IsAdmin) return;
        if (!AccessibleBStoreIds.Contains(bStoreId))
            throw new BStoreAccessException(UserId, bStoreId);
    }

    public void EnforceBStoreOwnership(int bStoreId)
    {
        if (IsAdmin) return;
        if (!AccessibleBStoreIds.Contains(bStoreId))
            throw new BStoreAccessException(UserId, bStoreId);
    }
}
