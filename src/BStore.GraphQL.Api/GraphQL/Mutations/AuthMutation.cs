using BStore.GraphQL.Api.Common;
using BStore.GraphQL.Api.GraphQL.Types;
using BStore.GraphQL.Api.Services;
using HotChocolate;
using Microsoft.Extensions.Logging;

namespace BStore.GraphQL.Api.GraphQL.Mutations;

/// <summary>
/// Authentication mutations for B-store token operations.
/// Extends the main <see cref="BStoreMutation"/> root.
/// </summary>
/// <remarks>
/// Maps to: POST /v2/b-stores/validate-token { Token: string }
/// </remarks>
[ExtendObjectType(typeof(BStoreMutation))]
public sealed class AuthMutation(ILogger<AuthMutation> logger)
{
    // ── POST /v2/b-stores/validate-token ─────────────────────────────────────

    /// <summary>
    /// Validates a B-store access / impersonation token.
    /// Returns SSO access details on success.
    /// </summary>
    // [Authorize]
    public async Task<BStoreValidateTokenResult> BStoreValidateToken(
        string token,
        [Service] IAuthApiClient client,
        CancellationToken ct)
    {
        logger.LogInformation("BStoreValidateToken called");
        try
        {
            var result = await client.ValidateTokenAsync(token, ct);
            return result ?? new BStoreValidateTokenResult
            {
                HasError     = true,
                ErrorMessage = "No response from authentication service."
            };
        }
        catch (Exception ex)
        {
            if (ex is GraphQLException) throw;
            logger.LogError(ex, "BStoreValidateToken unexpected error");
            throw ErrorMapper.ToGraphQL(ex);
        }
    }
}
