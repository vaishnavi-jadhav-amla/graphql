namespace BStore.GraphQL.Api.Auth;

/// <summary>
/// Per-request security context for B-store seller operations.
/// Enforces IDOR guards — a user can only access their own B-store(s).
/// </summary>
public interface IBStoreAuthContext
{
    /// <summary>The authenticated user's id (from JWT claim).</summary>
    int UserId { get; }

    /// <summary>The B-store id(s) the user owns or manages.</summary>
    IReadOnlyList<int> AccessibleBStoreIds { get; }

    /// <summary>True if the user has the Admin role (bypasses IDOR checks).</summary>
    bool IsAdmin { get; }

    /// <summary>
    /// Throws <see cref="Diagnostics.Exceptions.BStoreAccessException"/> if the user
    /// does not have access to the specified B-store.
    /// </summary>
    void EnforceBStoreAccess(int bStoreId);

    /// <summary>
    /// Throws <see cref="Diagnostics.Exceptions.BStoreAccessException"/> if the user
    /// is not an owner/admin of the specified B-store.
    /// </summary>
    void EnforceBStoreOwnership(int bStoreId);
}
