namespace BStore.GraphQL.Api.Auth.FieldPermissions;

/// <summary>
/// Declarative attribute for field-level access control on GraphQL type properties.
/// When applied, only users with the specified roles or permissions can read the field.
/// Unauthorised users receive <c>null</c> instead of the real value.
///
/// <para>Works like a .NET <c>[Authorize]</c> attribute but at the field/property level,
/// enforced by <see cref="FieldPermissionMiddleware"/> via <see cref="FieldPermissionTypeInterceptor"/>.</para>
/// </summary>
/// <example>
/// <code>
/// // Require Admin or BStoreOwner role:
/// [RequirePermission(Roles = [AuthConstants.RoleAdmin, AuthConstants.RoleBStoreOwner])]
/// public decimal CostPrice { get; set; }
///
/// // Require a named permission:
/// [RequirePermission(Permissions = ["inventory.read"])]
/// public int Stock { get; set; }
///
/// // Require authentication only (any role):
/// [RequirePermission]
/// public string InternalNotes { get; set; }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class RequirePermissionAttribute : Attribute
{
    /// <summary>
    /// Roles that grant access to this field. If empty, any authenticated user is allowed
    /// (unless <see cref="Permissions"/> is set).
    /// Multiple roles are evaluated with OR logic — any one role grants access.
    /// Use <see cref="AuthConstants"/> role constants.
    /// </summary>
    public string[] Roles { get; set; } = [];

    /// <summary>
    /// Named permissions that grant access to this field.
    /// Multiple permissions are evaluated with OR logic — any one permission grants access.
    /// When both <see cref="Roles"/> and <see cref="Permissions"/> are specified,
    /// the user must satisfy at least one role OR at least one permission.
    /// </summary>
    public string[] Permissions { get; set; } = [];

    /// <summary>
    /// If <c>true</c>, the field is completely removed from the schema for unauthorized users
    /// (not just nulled). Default is <c>false</c> (field returns null for unauthorized users).
    /// </summary>
    public bool HideFromSchema { get; set; }

    /// <summary>
    /// Custom error message returned in the GraphQL error extensions when access is denied.
    /// Defaults to a generic "You do not have permission to access this field."
    /// </summary>
    public string? DeniedMessage { get; set; }
}
