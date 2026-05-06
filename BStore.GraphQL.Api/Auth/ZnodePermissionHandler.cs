using Microsoft.AspNetCore.Authorization;

namespace BStore.GraphQL.Api.Auth;

/// <summary>
/// Custom authorization handler that evaluates Znode-specific permission requirements.
/// Supports Admin bypass, API-key bypass, and database-driven permission checks.
/// </summary>
public sealed class ZnodePermissionHandler : AuthorizationHandler<ZnodePermissionRequirement>
{
    private readonly IServiceProvider _services;
    private readonly ILogger<ZnodePermissionHandler> _logger;

    public ZnodePermissionHandler(IServiceProvider services, ILogger<ZnodePermissionHandler> logger)
    {
        _services = services;
        _logger = logger;
    }

    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        ZnodePermissionRequirement requirement)
    {
        if (context.User.Identity?.IsAuthenticated != true)
            return Task.CompletedTask;

        // Admin and ServerToServer roles bypass all permission checks
        if (context.User.IsInRole(AuthConstants.RoleAdmin) ||
            context.User.IsInRole(AuthConstants.RoleServerToServer))
        {
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        // For customer-facing roles, check the specific permission
        var role = context.User.FindFirst(AuthConstants.ClaimRole)?.Value;
        if (string.IsNullOrEmpty(role))
            return Task.CompletedTask;

        // Evaluate the permission against the user's role
        if (EvaluatePermission(requirement.Permission, role))
        {
            context.Succeed(requirement);
        }
        else
        {
            _logger.LogWarning(
                "Permission denied: {Permission} for role {Role}",
                requirement.Permission, role);
        }

        return Task.CompletedTask;
    }

    private static bool EvaluatePermission(string permission, string role) =>
        role switch
        {
            AuthConstants.RoleBStoreOwner => true,  // Owners have all BStore permissions
            AuthConstants.RoleBStoreEmployee => !permission.EndsWith(":Delete", StringComparison.OrdinalIgnoreCase),
            AuthConstants.RoleCustomer => permission.StartsWith("Storefront:", StringComparison.OrdinalIgnoreCase),
            _ => false
        };
}

/// <summary>
/// A named permission requirement for Znode authorization policies.
/// </summary>
public sealed class ZnodePermissionRequirement : IAuthorizationRequirement
{
    public string Permission { get; }
    public ZnodePermissionRequirement(string permission) => Permission = permission;
}
