namespace BStore.GraphQL.Api.Configuration;

/// <summary>
/// Rate limiting configuration. Bound to the <c>RateLimit</c> configuration section.
/// </summary>
public sealed class RateLimitSettings
{
    public const string Section = "RateLimit";

    /// <summary>Enable rate limiting middleware.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Global requests per minute per IP.</summary>
    public int GlobalRequestsPerMinute { get; set; } = 60;

    /// <summary>Mutation requests per minute per IP.</summary>
    public int MutationRequestsPerMinute { get; set; } = 20;

    /// <summary>Auth (login/register) requests per minute per IP.</summary>
    public int AuthRequestsPerMinute { get; set; } = 10;

    /// <summary>Admin endpoint requests per minute per IP.</summary>
    public int AdminRequestsPerMinute { get; set; } = 30;
}
