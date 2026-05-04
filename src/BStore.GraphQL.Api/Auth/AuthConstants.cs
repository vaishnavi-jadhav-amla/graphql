namespace BStore.GraphQL.Api.Auth;

/// <summary>
/// Centralised authentication/authorization constants used across policies, handlers, and resolvers.
/// </summary>
public static class AuthConstants
{
    // --- Authentication Schemes ---
    public const string JwtScheme = "Bearer";
    public const string ApiKeyScheme = "ApiKey";
    public const string PolicyScheme = "BStorePolicy";

    // --- Authorization Policies ---
    public const string PolicyAdminOnly = "AdminOnly";
    public const string PolicyAuthenticated = "Authenticated";
    public const string PolicyServerToServer = "ServerToServer";
    public const string PolicyBStoreAccess = "BStoreAccess";
    public const string PolicyBStoreAdmin = "BStoreAdmin";
    public const string PolicyPortalDynamic = "PortalDynamic";

    // --- Claim Types ---
    public const string ClaimUserId = "uid";
    public const string ClaimPortalId = "portalId";
    public const string ClaimAccountId = "accountId";
    public const string ClaimBStoreId = "bStoreId";
    public const string ClaimBStoreRole = "bStoreRole";
    public const string ClaimRole = "role";

    // --- Roles ---
    public const string RoleAdmin = "Admin";
    public const string RoleServerToServer = "ServerToServer";
    public const string RoleBStoreOwner = "BStoreOwner";
    public const string RoleBStoreEmployee = "BStoreEmployee";
    public const string RoleCustomer = "Customer";

    // --- Headers ---
    public const string ApiKeyHeader = "X-API-Key";
}
