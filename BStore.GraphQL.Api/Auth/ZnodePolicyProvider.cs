using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace BStore.GraphQL.Api.Auth;

/// <summary>
/// Dynamic policy provider that creates Znode permission-based policies on demand.
/// Policies prefixed with <c>Znode:</c> are resolved dynamically.
/// </summary>
public sealed class ZnodePolicyProvider : IAuthorizationPolicyProvider
{
    private const string ZnodePrefix = "Znode:";
    private readonly DefaultAuthorizationPolicyProvider _fallback;

    public ZnodePolicyProvider(IOptions<AuthorizationOptions> options)
    {
        _fallback = new DefaultAuthorizationPolicyProvider(options);
    }

    public Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (policyName.StartsWith(ZnodePrefix, StringComparison.OrdinalIgnoreCase))
        {
            var permission = policyName; // e.g. "Znode:BStore:View"
            var policy = new AuthorizationPolicyBuilder()
                .AddRequirements(new ZnodePermissionRequirement(permission))
                .Build();
            return Task.FromResult<AuthorizationPolicy?>(policy);
        }

        return _fallback.GetPolicyAsync(policyName);
    }

    public Task<AuthorizationPolicy> GetDefaultPolicyAsync() =>
        _fallback.GetDefaultPolicyAsync();

    public Task<AuthorizationPolicy?> GetFallbackPolicyAsync() =>
        _fallback.GetFallbackPolicyAsync();
}
