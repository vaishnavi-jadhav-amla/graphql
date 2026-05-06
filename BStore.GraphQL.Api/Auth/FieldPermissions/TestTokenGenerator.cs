using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using BStore.GraphQL.Api.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace BStore.GraphQL.Api.Auth.FieldPermissions;

/// <summary>
/// Token generation endpoints for testing field-level permissions.
/// <para><b>Security:</b> All endpoints require <see cref="AuthConstants.PolicyAdminOnly"/>
/// (Admin JWT or API key). Safe for production — only admins can generate tokens.</para>
/// <para>Routes: <c>/api/auth/token</c>, <c>/api/auth/token/all</c>, <c>/api/auth/roles</c></para>
/// </summary>
public static class AuthTokenEndpoints
{
    private static readonly string[] AllRoles =
    [
        AuthConstants.RoleAdmin,
        AuthConstants.RoleServerToServer,
        AuthConstants.RoleBStoreOwner,
        AuthConstants.RoleBStoreEmployee,
        AuthConstants.RoleCustomer
    ];

    /// <summary>
    /// Maps admin-secured token generation endpoints onto the application.
    /// <para>In <b>Development</b>: endpoints are open (no auth required) for easy testing.</para>
    /// <para>In <b>Production</b>: endpoints require AdminOnly policy (Admin JWT or API key).</para>
    /// </summary>
    public static WebApplication MapAuthTokenEndpoints(this WebApplication app)
    {
        var isDev = app.Environment.IsDevelopment();

        var group = app.MapGroup("/api/auth")
            .WithTags("Auth Tokens");

        // In dev: open access for bootstrapping. In prod: admin-only.
        if (isDev)
            group.AllowAnonymous();
        else
            group.RequireAuthorization(AuthConstants.PolicyAdminOnly);

        // ──────────────────────────────────────────────────────────────
        // GET /api/auth/token?role=BStoreOwner&bStoreId=1242
        // Generates a single JWT for the requested role.
        // ──────────────────────────────────────────────────────────────
        group.MapGet("/token", (
            string? role,
            int? userId,
            int? portalId,
            int? bStoreId,
            string? permissions,
            int? expiresInMinutes,
            HttpRequest request,
            IOptions<AuthSettings> authSettings) =>
        {
            var settings = authSettings.Value;
            if (string.IsNullOrWhiteSpace(settings.SigningKeyBase64))
                return Results.Problem(
                    "Auth:SigningKeyBase64 is not configured in appsettings.",
                    statusCode: 500);

            var effectiveRole = role ?? AuthConstants.RoleCustomer;
            if (!AllRoles.Contains(effectiveRole, StringComparer.OrdinalIgnoreCase))
                return Results.BadRequest(new
                {
                    error = $"Unknown role '{effectiveRole}'.",
                    availableRoles = AllRoles
                });

            var effectiveUserId = userId ?? 1;
            var effectivePortalId = portalId ?? 1;
            var effectiveBStoreId = bStoreId ?? 100;
            var effectiveExpiry = Math.Clamp(expiresInMinutes ?? 120, 1, 1440); // max 24h
            var permissionList = string.IsNullOrWhiteSpace(permissions)
                ? Array.Empty<string>()
                : permissions.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            var token = GenerateToken(
                settings.SigningKeyBase64,
                effectiveRole,
                permissionList,
                effectiveUserId,
                effectivePortalId,
                effectiveBStoreId,
                effectiveExpiry);

            var baseUrl = $"{request.Scheme}://{request.Host}";

            return Results.Json(new
            {
                token,
                role = effectiveRole,
                userId = effectiveUserId,
                portalId = effectivePortalId,
                bStoreId = effectiveBStoreId,
                permissions = permissionList,
                expiresInMinutes = effectiveExpiry,
                usage = new
                {
                    header = $"Authorization: Bearer {token}",
                    curlBStoreDetails = $"curl -X POST {baseUrl}/graphql -H \"Content-Type: application/json\" -H \"Authorization: Bearer {token}\" -d '{{\"query\":\"{{ bStore(storeId: {effectiveBStoreId}) {{ portalId storeName globalAttributes externalId }} }}\"}}'",
                    curlProduct = $"curl -X POST {baseUrl}/graphql -H \"Content-Type: application/json\" -H \"Authorization: Bearer {token}\" -d '{{\"query\":\"{{ product(id: 1) {{ id title price discountPercentage stock }} }}\"}}'",
                }
            });
        });

        // ──────────────────────────────────────────────────────────────
        // GET /api/auth/token/all?bStoreId=1242
        // Generates JWT tokens for ALL roles at once.
        // ──────────────────────────────────────────────────────────────
        group.MapGet("/token/all", (
            int? portalId,
            int? bStoreId,
            int? expiresInMinutes,
            HttpRequest request,
            IOptions<AuthSettings> authSettings) =>
        {
            var settings = authSettings.Value;
            if (string.IsNullOrWhiteSpace(settings.SigningKeyBase64))
                return Results.Problem(
                    "Auth:SigningKeyBase64 is not configured in appsettings.",
                    statusCode: 500);

            var effectivePortalId = portalId ?? 1;
            var effectiveBStoreId = bStoreId ?? 100;
            var effectiveExpiry = Math.Clamp(expiresInMinutes ?? 120, 1, 1440);
            var baseUrl = $"{request.Scheme}://{request.Host}";

            var tokenStrings = new Dictionary<string, string>();
            var tokens = new Dictionary<string, object>();
            var userIdCounter = 1;

            foreach (var r in AllRoles)
            {
                var token = GenerateToken(
                    settings.SigningKeyBase64, r, [],
                    userIdCounter++, effectivePortalId, effectiveBStoreId, effectiveExpiry);

                tokenStrings[r] = token;
                tokens[r] = new { token, header = $"Authorization: Bearer {token}" };
            }

            // Bonus: BStoreEmployee with inventory.read permission
            var empWithPerm = GenerateToken(
                settings.SigningKeyBase64,
                AuthConstants.RoleBStoreEmployee,
                ["inventory.read"],
                userIdCounter, effectivePortalId, effectiveBStoreId, effectiveExpiry);

            tokens["BStoreEmployee+inventory.read"] = new
            {
                token = empWithPerm,
                header = $"Authorization: Bearer {empWithPerm}"
            };

            var bStoreQuery = $"{{ bStore(storeId: {effectiveBStoreId}) {{ portalId storeName domainURL isActive globalAttributes externalId }} }}";

            return Results.Json(new
            {
                portalId = effectivePortalId,
                bStoreId = effectiveBStoreId,
                expiresInMinutes = effectiveExpiry,
                tokens,
                testQueries = new
                {
                    bStoreDetails = bStoreQuery,
                    product = "{ product(id: 1) { id title price discountPercentage stock brand } }",
                    productList = "{ productList(limit: 3) { total products { id title discountPercentage stock } } }"
                },
                fieldPermissionMatrix = new[]
                {
                    new { type = "ProductRow",    field = "discountPercentage", allowedRoles = "Admin, BStoreOwner",                allowedPermissions = "(none)" },
                    new { type = "ProductRow",    field = "stock",              allowedRoles = "Admin, BStoreOwner, BStoreEmployee", allowedPermissions = "inventory.read" },
                    new { type = "BStoreDetails", field = "globalAttributes",   allowedRoles = "Admin, BStoreOwner",                allowedPermissions = "(none)" },
                    new { type = "BStoreDetails", field = "externalId",         allowedRoles = "Admin, ServerToServer",             allowedPermissions = "(none)" },
                },
                sampleCurls = new
                {
                    admin    = $"curl -X POST {baseUrl}/graphql -H \"Content-Type: application/json\" -H \"Authorization: Bearer {tokenStrings["Admin"]}\" -d '{{\"query\":\"{bStoreQuery}\"}}'",
                    customer = $"curl -X POST {baseUrl}/graphql -H \"Content-Type: application/json\" -H \"Authorization: Bearer {tokenStrings["Customer"]}\" -d '{{\"query\":\"{bStoreQuery}\"}}'",
                }
            });
        });

        // ──────────────────────────────────────────────────────────────
        // GET /api/auth/roles
        // Lists all available roles and the protected fields matrix.
        // ──────────────────────────────────────────────────────────────
        group.MapGet("/roles", () => Results.Json(new
        {
            roles = AllRoles,
            usage = new
            {
                singleToken = "GET /api/auth/token?role=BStoreOwner&bStoreId=1242",
                allTokens = "GET /api/auth/token/all?bStoreId=1242",
                parameters = new
                {
                    role = "One of: Admin, ServerToServer, BStoreOwner, BStoreEmployee, Customer",
                    userId = "User ID (default: 1)",
                    portalId = "Portal ID (default: 1)",
                    bStoreId = "B-store ID (default: 100)",
                    permissions = "Comma-separated permission claims (e.g., inventory.read,pricing.write)",
                    expiresInMinutes = "Token lifetime 1-1440 (default: 120)"
                }
            },
            fieldPermissionMatrix = new[]
            {
                new { type = "ProductRow",    field = "discountPercentage", allowedRoles = "Admin, BStoreOwner",                allowedPermissions = "(none)" },
                new { type = "ProductRow",    field = "stock",              allowedRoles = "Admin, BStoreOwner, BStoreEmployee", allowedPermissions = "inventory.read" },
                new { type = "BStoreDetails", field = "globalAttributes",   allowedRoles = "Admin, BStoreOwner",                allowedPermissions = "(none)" },
                new { type = "BStoreDetails", field = "externalId",         allowedRoles = "Admin, ServerToServer",             allowedPermissions = "(none)" },
            }
        }));

        return app;
    }

    private static string GenerateToken(
        string signingKeyBase64,
        string role,
        string[] permissions,
        int userId,
        int portalId,
        int bStoreId,
        int expiresInMinutes)
    {
        var keyBytes = Convert.FromBase64String(signingKeyBase64);
        var securityKey = new SymmetricSecurityKey(keyBytes);
        var creds = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(AuthConstants.ClaimUserId, userId.ToString()),
            new(AuthConstants.ClaimPortalId, portalId.ToString()),
            new(AuthConstants.ClaimBStoreId, bStoreId.ToString()),
            new(AuthConstants.ClaimRole, role),
            new(ClaimTypes.Role, role)
        };

        foreach (var p in permissions)
            claims.Add(new Claim("permission", p));

        var token = new JwtSecurityToken(
            issuer: "BStore.GraphQL",
            audience: "BStore.GraphQL.Api",
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: DateTime.UtcNow.AddMinutes(expiresInMinutes),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
