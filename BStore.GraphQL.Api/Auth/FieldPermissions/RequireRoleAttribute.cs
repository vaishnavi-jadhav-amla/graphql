using HotChocolate.Authorization;

namespace BStore.GraphQL.Api.Auth.FieldPermissions;

/// <summary>
/// Convenience attributes for resolver-level (entire operation) access control.
/// These wrap HotChocolate's <see cref="AuthorizeAttribute"/> with pre-configured roles,
/// keeping the role definitions consistent with <see cref="RequirePermissionAttribute"/>
/// used at the field level.
///
/// <para><b>Usage:</b> Place on resolver methods to restrict the entire operation.
/// Unlike <see cref="RequirePermissionAttribute"/> (which nulls individual fields),
/// these block the entire query/mutation and return an authorization error.</para>
/// </summary>
/// <example>
/// <code>
/// [RequireAdmin]                          // Admin or ServerToServer only
/// public async Task&lt;bool&gt; DeleteUser(...)
///
/// [RequireBStoreAdmin]                    // Admin, BStoreOwner, or ServerToServer
/// public async Task&lt;bool&gt; BStoreUpdate(...)
///
/// [RequireAuthenticated]                  // Any authenticated user
/// public async Task&lt;ProductRow?&gt; Product(...)
///
/// [RequireRole(AuthConstants.RoleAdmin, AuthConstants.RoleBStoreOwner)]  // Custom role list
/// public async Task&lt;bool&gt; SomeOperation(...)
/// </code>
/// </example>
public static class ResolverAccessAttributes
{
    // Intentionally left empty — attributes are defined below as standalone classes.
    // This class exists only to anchor the shared XML doc above.
}

/// <summary>
/// Restricts a resolver to <b>Admin</b> or <b>ServerToServer</b> roles.
/// Maps to <see cref="AuthConstants.PolicyAdminOnly"/>.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class RequireAdminAttribute : AuthorizeAttribute
{
    public RequireAdminAttribute()
    {
        Policy = AuthConstants.PolicyAdminOnly;
    }
}

/// <summary>
/// Restricts a resolver to <b>Admin</b>, <b>BStoreOwner</b>, or <b>ServerToServer</b> roles.
/// Maps to <see cref="AuthConstants.PolicyBStoreAdmin"/>.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class RequireBStoreAdminAttribute : AuthorizeAttribute
{
    public RequireBStoreAdminAttribute()
    {
        Policy = AuthConstants.PolicyBStoreAdmin;
    }
}

/// <summary>
/// Restricts a resolver to any authenticated user (any role).
/// Maps to <see cref="AuthConstants.PolicyAuthenticated"/>.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class RequireAuthenticatedAttribute : AuthorizeAttribute
{
    public RequireAuthenticatedAttribute()
    {
        Policy = AuthConstants.PolicyAuthenticated;
    }
}

/// <summary>
/// Restricts a resolver to users with a B-store claim (<c>bStoreId</c> in JWT).
/// Maps to <see cref="AuthConstants.PolicyBStoreAccess"/>.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class RequireBStoreAccessAttribute : AuthorizeAttribute
{
    public RequireBStoreAccessAttribute()
    {
        Policy = AuthConstants.PolicyBStoreAccess;
    }
}

/// <summary>
/// Restricts a resolver to one or more specific roles.
/// Use when the built-in convenience attributes don't fit.
/// </summary>
/// <example>
/// <code>
/// [RequireRole(AuthConstants.RoleAdmin, AuthConstants.RoleBStoreOwner, AuthConstants.RoleBStoreEmployee)]
/// public async Task&lt;bool&gt; SomeOperation(...)
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class RequireRoleAttribute : AuthorizeAttribute
{
    public RequireRoleAttribute(params string[] roles)
    {
        Roles = roles;
    }
}
