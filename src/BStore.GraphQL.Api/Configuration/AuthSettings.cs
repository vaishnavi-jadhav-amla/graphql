namespace BStore.GraphQL.Api.Configuration;

/// <summary>
/// Authentication configuration. Bound to the <c>Auth</c> configuration section.
/// </summary>
public sealed class AuthSettings
{
    public const string Section = "Auth";

    /// <summary>JWT issuer (iss claim).</summary>
    public string Issuer { get; set; } = "BStore.GraphQL";

    /// <summary>JWT audience (aud claim).</summary>
    public string Audience { get; set; } = "BStore.GraphQL.Api";

    /// <summary>Base64-encoded symmetric signing key for JWT. Must be at least 32 bytes.</summary>
    public string SigningKeyBase64 { get; set; } = "";

    /// <summary>Access token lifetime in minutes.</summary>
    public int AccessTokenLifetimeMinutes { get; set; } = 30;

    /// <summary>Refresh token lifetime in days.</summary>
    public int RefreshTokenLifetimeDays { get; set; } = 7;

    /// <summary>Whether to require HTTPS for token metadata endpoints.</summary>
    public bool RequireHttpsMetadata { get; set; } = true;

    /// <summary>Valid API keys for server-to-server authentication.</summary>
    public string[] ApiKeys { get; set; } = [];
}
