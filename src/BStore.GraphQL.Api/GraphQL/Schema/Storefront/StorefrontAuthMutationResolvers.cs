using BStore.GraphQL.Api.Common;
using BStore.GraphQL.Api.GraphQL.Types;
using BStore.GraphQL.Api.Services;
using HotChocolate;
using HotChocolate.Types;

namespace BStore.GraphQL.Api.GraphQL.Schema.Storefront;

/// <summary>
/// Storefront authentication mutations — token validation for customer-facing auth.
/// </summary>
[ExtendObjectType(typeof(StorefrontMutation))]
public sealed class StorefrontAuthMutationResolvers(ILogger<StorefrontAuthMutationResolvers> logger)
{
    [GraphQLDescription("Validate a B-store access/impersonation token (storefront).")]
    public async Task<BStoreValidateTokenResult> BStoreValidateToken(
        string token,
        [Service] IAuthApiClient client,
        CancellationToken ct)
    {
        logger.LogInformation("StorefrontAuthMutation: BStoreValidateToken called");
        try
        {
            var result = await client.ValidateTokenAsync(token, ct);
            return result ?? new BStoreValidateTokenResult
            {
                HasError = true,
                ErrorMessage = "No response from authentication service."
            };
        }
        catch (Exception ex)
        {
            if (ex is GraphQLException) throw;
            logger.LogError(ex, "StorefrontAuthMutation: BStoreValidateToken unexpected error");
            throw ErrorMapper.ToGraphQL(ex);
        }
    }
}
