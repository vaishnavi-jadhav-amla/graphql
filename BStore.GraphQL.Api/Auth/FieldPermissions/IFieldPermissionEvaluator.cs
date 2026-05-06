using System.Security.Claims;

namespace BStore.GraphQL.Api.Auth.FieldPermissions;

/// <summary>
/// Evaluates whether a user has permission to access a field protected by
/// <see cref="RequirePermissionAttribute"/>. Implement this interface to plug in
/// custom permission stores (database, external API, feature flags, etc.).
/// </summary>
public interface IFieldPermissionEvaluator
{
    /// <summary>
    /// Determines whether the given user principal satisfies the field permission requirements.
    /// </summary>
    /// <param name="user">The current user's claims principal from the HTTP context.</param>
    /// <param name="requirement">The permission requirement derived from the attribute.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><c>true</c> if access is granted; <c>false</c> otherwise.</returns>
    ValueTask<bool> IsAuthorizedAsync(
        ClaimsPrincipal? user,
        FieldPermissionRequirement requirement,
        CancellationToken ct);
}

/// <summary>
/// Encapsulates the permission requirements for a single field, derived from
/// <see cref="RequirePermissionAttribute"/>.
/// </summary>
public sealed class FieldPermissionRequirement
{
    /// <summary>Roles that grant access (OR logic).</summary>
    public IReadOnlyList<string> Roles { get; init; } = [];

    /// <summary>Named permissions that grant access (OR logic).</summary>
    public IReadOnlyList<string> Permissions { get; init; } = [];

    /// <summary>The GraphQL field name for diagnostics/logging.</summary>
    public string FieldName { get; init; } = "";

    /// <summary>The parent GraphQL type name for diagnostics/logging.</summary>
    public string TypeName { get; init; } = "";

    /// <summary>Custom denial message from the attribute.</summary>
    public string? DeniedMessage { get; init; }

    /// <summary>Whether the requirement is "any authenticated user" (no roles or permissions specified).</summary>
    public bool RequiresAuthenticationOnly => Roles.Count == 0 && Permissions.Count == 0;
}
