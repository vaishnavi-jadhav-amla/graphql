using BStore.GraphQL.Api.Diagnostics.Exceptions;

namespace BStore.GraphQL.Api.Auth;

/// <summary>
/// Base ownership guard. Validates that the authenticated user has access
/// to the requested resource before the resolver executes.
/// </summary>
public interface IOwnershipGuard
{
    /// <summary>Enforce that the caller owns or has access to the resource.</summary>
    void Enforce(int resourceId);
}

/// <summary>
/// B-store ownership guard. Ensures a user can only access B-stores they own/manage.
/// </summary>
public sealed class BStoreOwnershipGuard : IOwnershipGuard
{
    private readonly IBStoreAuthContext _authContext;
    private readonly ILogger<BStoreOwnershipGuard> _logger;

    public BStoreOwnershipGuard(IBStoreAuthContext authContext, ILogger<BStoreOwnershipGuard> logger)
    {
        _authContext = authContext;
        _logger = logger;
    }

    public void Enforce(int bStoreId)
    {
        if (_authContext.IsAdmin) return;

        if (!_authContext.AccessibleBStoreIds.Contains(bStoreId))
        {
            _logger.LogWarning(
                "IDOR blocked: user {UserId} attempted to access B-store {BStoreId}",
                _authContext.UserId, bStoreId);
            throw new BStoreAccessException(_authContext.UserId, bStoreId);
        }
    }
}

/// <summary>
/// Admin portal ownership guard. Ensures admin users stay within their portal boundary.
/// </summary>
public sealed class AdminOwnershipGuard : IOwnershipGuard
{
    private readonly IAdminAuthContext _authContext;
    private readonly ILogger<AdminOwnershipGuard> _logger;

    public AdminOwnershipGuard(IAdminAuthContext authContext, ILogger<AdminOwnershipGuard> logger)
    {
        _authContext = authContext;
        _logger = logger;
    }

    public void Enforce(int portalId)
    {
        if (!_authContext.IsAdmin) return;

        if (_authContext.PortalId != 0 && _authContext.PortalId != portalId)
        {
            _logger.LogWarning(
                "Cross-tenant blocked: admin {UserId} (portal {CallerPortal}) attempted to access portal {TargetPortal}",
                _authContext.UserId, _authContext.PortalId, portalId);
            throw new CrossTenantAccessException(_authContext.PortalId, portalId);
        }
    }
}

/// <summary>
/// Storefront account ownership guard. Ensures customers can only access their own data.
/// </summary>
public sealed class StorefrontOwnershipGuard : IOwnershipGuard
{
    private readonly IStorefrontAuthContext _authContext;

    public StorefrontOwnershipGuard(IStorefrontAuthContext authContext) => _authContext = authContext;

    public void Enforce(int accountId)
    {
        if (_authContext.AccountId != accountId)
            throw new UnauthorizedAccessException(
                $"Account {accountId} does not belong to the authenticated user.");
    }
}
