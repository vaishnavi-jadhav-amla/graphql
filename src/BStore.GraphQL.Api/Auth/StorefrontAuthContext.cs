using System.Security.Claims;

namespace BStore.GraphQL.Api.Auth;

/// <summary>
/// Scoped storefront auth context. Enforces that customers can only access their own data.
/// </summary>
public sealed class StorefrontAuthContext : IStorefrontAuthContext
{
    public int UserId { get; }
    public int AccountId { get; }
    public int PortalId { get; }
    public bool IsAuthenticated { get; }

    public StorefrontAuthContext(IHttpContextAccessor httpContextAccessor)
    {
        var user = httpContextAccessor.HttpContext?.User;
        IsAuthenticated = user?.Identity?.IsAuthenticated == true;

        if (!IsAuthenticated) return;

        UserId = int.TryParse(user?.FindFirstValue(AuthConstants.ClaimUserId), out var uid) ? uid : 0;
        AccountId = int.TryParse(user.FindFirstValue(AuthConstants.ClaimAccountId), out var aid) ? aid : 0;
        PortalId = int.TryParse(user.FindFirstValue(AuthConstants.ClaimPortalId), out var pid) ? pid : 0;
    }

    public void EnforceAccountOwnership(int accountId)
    {
        if (AccountId != accountId)
            throw new UnauthorizedAccessException(
                $"Account {accountId} does not belong to the authenticated user.");
    }
}
