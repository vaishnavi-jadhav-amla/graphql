using System.Security.Claims;
using BStore.GraphQL.Api.Configuration;
using Microsoft.Extensions.Options;

namespace BStore.GraphQL.Api.Auth.FieldPermissions;

/// <summary>
/// Default implementation of <see cref="IFieldPermissionEvaluator"/> that checks
/// the user's JWT claims against the field requirement. Respects
/// <see cref="PermissionSettings"/> bypass rules (admin bypass, API key bypass, disabled checks).
/// </summary>
public sealed class DefaultFieldPermissionEvaluator(
    IOptions<PermissionSettings> permissionOptions,
    ILogger<DefaultFieldPermissionEvaluator> logger) : IFieldPermissionEvaluator
{
    private readonly PermissionSettings _settings = permissionOptions.Value;

    public ValueTask<bool> IsAuthorizedAsync(
        ClaimsPrincipal? user,
        FieldPermissionRequirement requirement,
        CancellationToken ct)
    {
        // --- Global bypasses (from PermissionSettings) ---

        // Wildcard disable — development only.
        if (_settings.DisabledChecks.Contains("*"))
            return ValueTask.FromResult(true);

        // Per-field disable (e.g., "ProductRow.CostPrice").
        var checkName = $"{requirement.TypeName}.{requirement.FieldName}";
        if (_settings.DisabledChecks.Contains(checkName))
            return ValueTask.FromResult(true);

        // Not authenticated → deny.
        if (user?.Identity?.IsAuthenticated != true)
        {
            logger.LogDebug(
                "Field permission denied (unauthenticated) | Type:{Type} Field:{Field}",
                requirement.TypeName, requirement.FieldName);
            return ValueTask.FromResult(false);
        }

        // Admin bypass.
        if (_settings.AdminRoleBypassAll &&
            (user.IsInRole(AuthConstants.RoleAdmin) || user.IsInRole(AuthConstants.RoleServerToServer)))
        {
            return ValueTask.FromResult(true);
        }

        // API key bypass (ServerToServer role implies API key auth).
        if (_settings.ApiKeyBypassAll && user.IsInRole(AuthConstants.RoleServerToServer))
        {
            return ValueTask.FromResult(true);
        }

        // --- Attribute-only authentication check (no roles/permissions specified) ---
        if (requirement.RequiresAuthenticationOnly)
            return ValueTask.FromResult(true);

        // --- Role check (OR logic) ---
        foreach (var role in requirement.Roles)
        {
            if (user.IsInRole(role))
                return ValueTask.FromResult(true);
        }

        // --- Permission claim check (OR logic) ---
        // Permissions are checked against the "permission" claim type in the JWT.
        foreach (var permission in requirement.Permissions)
        {
            if (user.HasClaim("permission", permission))
                return ValueTask.FromResult(true);
        }

        logger.LogDebug(
            "Field permission denied | Type:{Type} Field:{Field} User:{UserId} RequiredRoles:{Roles} RequiredPermissions:{Permissions}",
            requirement.TypeName,
            requirement.FieldName,
            user.FindFirstValue(AuthConstants.ClaimUserId) ?? "unknown",
            string.Join(",", requirement.Roles),
            string.Join(",", requirement.Permissions));

        return ValueTask.FromResult(false);
    }
}
