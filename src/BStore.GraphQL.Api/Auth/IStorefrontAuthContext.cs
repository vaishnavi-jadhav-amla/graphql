namespace BStore.GraphQL.Api.Auth;

/// <summary>
/// Per-request security context for storefront/customer operations.
/// Customers can only access their own cart, orders, addresses, and account.
/// </summary>
public interface IStorefrontAuthContext
{
    int UserId { get; }
    int AccountId { get; }
    int PortalId { get; }
    bool IsAuthenticated { get; }

    /// <summary>
    /// Throws <see cref="UnauthorizedAccessException"/> if the current user
    /// does not own the specified account.
    /// </summary>
    void EnforceAccountOwnership(int accountId);
}
