using BStore.GraphQL.Api.GraphQL.Types;

namespace BStore.GraphQL.Api.Services;

/// <summary>
/// HTTP client contract for B-store authentication endpoints.
/// </summary>
public interface IAuthApiClient
{
    Task<BStoreValidateTokenResult?> ValidateTokenAsync(
        string token, CancellationToken ct = default);
}
