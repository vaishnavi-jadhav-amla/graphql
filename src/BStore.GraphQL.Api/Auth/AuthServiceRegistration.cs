using BStore.GraphQL.Api.Configuration;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;

namespace BStore.GraphQL.Api.Auth;

/// <summary>
/// Registers authentication schemes (JWT + API Key), authorization policies,
/// auth contexts (BStore, Admin, Storefront), and the token service.
/// </summary>
public static class AuthServiceRegistration
{
    public static WebApplicationBuilder AddBStoreAuth(this WebApplicationBuilder builder)
    {
        var authSection = builder.Configuration.GetSection(AuthSettings.Section);
        builder.Services.Configure<AuthSettings>(authSection);
        var authSettings = authSection.Get<AuthSettings>() ?? new AuthSettings();

        // --- Authentication Schemes ---
        var authBuilder = builder.Services.AddAuthentication(options =>
        {
            options.DefaultScheme = AuthConstants.PolicyScheme;
            options.DefaultChallengeScheme = AuthConstants.JwtScheme;
        });

        // Policy scheme: auto-selects JWT or API key based on request headers
        authBuilder.AddPolicyScheme(AuthConstants.PolicyScheme, "Auto-select JWT or API Key", options =>
        {
            options.ForwardDefaultSelector = context =>
            {
                if (context.Request.Headers.ContainsKey(AuthConstants.ApiKeyHeader))
                    return AuthConstants.ApiKeyScheme;
                return AuthConstants.JwtScheme;
            };
        });

        // JWT Bearer
        if (!string.IsNullOrWhiteSpace(authSettings.SigningKeyBase64))
        {
            var keyBytes = Convert.FromBase64String(authSettings.SigningKeyBase64);
            authBuilder.AddJwtBearer(AuthConstants.JwtScheme, options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = authSettings.Issuer,
                    ValidateAudience = true,
                    ValidAudience = authSettings.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(keyBytes),
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromMinutes(1)
                };
                options.RequireHttpsMetadata = authSettings.RequireHttpsMetadata;
            });
        }
        else
        {
            // Fallback: register a no-op JWT scheme so the app doesn't crash when JWT is not configured
            authBuilder.AddJwtBearer(AuthConstants.JwtScheme, options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = false,
                    ValidateAudience = false,
                    ValidateIssuerSigningKey = false,
                    ValidateLifetime = false,
                    RequireSignedTokens = false
                };
            });
        }

        // API Key
        authBuilder.AddScheme<AuthenticationSchemeOptions, ApiKeyAuthHandler>(
            AuthConstants.ApiKeyScheme, _ => { });

        // --- Authorization Policies ---
        builder.Services.AddAuthorization(options =>
        {
            options.AddPolicy(AuthConstants.PolicyAdminOnly, policy =>
                policy.RequireRole(AuthConstants.RoleAdmin, AuthConstants.RoleServerToServer));

            options.AddPolicy(AuthConstants.PolicyAuthenticated, policy =>
                policy.RequireAuthenticatedUser());

            options.AddPolicy(AuthConstants.PolicyServerToServer, policy =>
                policy.RequireRole(AuthConstants.RoleServerToServer));

            options.AddPolicy(AuthConstants.PolicyBStoreAccess, policy =>
                policy.RequireAuthenticatedUser()
                      .RequireClaim(AuthConstants.ClaimBStoreId));

            options.AddPolicy(AuthConstants.PolicyBStoreAdmin, policy =>
                policy.RequireRole(
                    AuthConstants.RoleAdmin,
                    AuthConstants.RoleBStoreOwner,
                    AuthConstants.RoleServerToServer));

            options.AddPolicy(AuthConstants.PolicyPortalDynamic, policy =>
                policy.RequireAuthenticatedUser());
        });

        // Dynamic policy provider for Znode: prefixed policies
        builder.Services.AddSingleton<IAuthorizationPolicyProvider, ZnodePolicyProvider>();
        builder.Services.AddSingleton<IAuthorizationHandler, ZnodePermissionHandler>();

        // --- Auth Contexts (per-request) ---
        builder.Services.AddScoped<ITokenService, JwtTokenService>();
        builder.Services.AddScoped<IBStoreAuthContext, BStoreAuthContext>();
        builder.Services.AddScoped<IAdminAuthContext, AdminAuthContext>();
        builder.Services.AddScoped<IStorefrontAuthContext, StorefrontAuthContext>();

        return builder;
    }
}
