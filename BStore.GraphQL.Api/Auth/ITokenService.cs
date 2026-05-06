using System.Security.Claims;

namespace BStore.GraphQL.Api.Auth;

/// <summary>
/// Issues and validates JWT tokens for B-store authentication.
/// </summary>
public interface ITokenService
{
    /// <summary>Generate an access token for the given claims.</summary>
    string GenerateAccessToken(IEnumerable<Claim> claims);

    /// <summary>Generate a refresh token (opaque, stored server-side).</summary>
    string GenerateRefreshToken();

    /// <summary>Validate a token and return the claims principal, or null if invalid.</summary>
    ClaimsPrincipal? ValidateToken(string token);
}
