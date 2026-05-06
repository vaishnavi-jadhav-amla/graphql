namespace BStore.GraphQL.Api.Configuration;

/// <summary>
/// Strongly-typed options for the downstream Znode REST API.
/// Bind from appsettings.json → "ZnodeApi".
/// </summary>
public sealed class ZnodeApiOptions
{
    public const string Section = "ZnodeApi";

    /// <summary>Base URL of the Znode v2 API (e.g. https://localhost:54546).</summary>
    public string BaseUrl { get; init; } = "";

    /// <summary>Domain name sent in the Authorization: Basic header.</summary>
    public string DomainName { get; init; } = "";

    /// <summary>Domain key sent in the Authorization: Basic header.</summary>
    public string DomainKey { get; init; } = "";

    /// <summary>HTTP timeout in seconds (default 30).</summary>
    public int TimeoutSeconds { get; init; } = 30;

    /// <summary>Builds the Basic auth header value: base64(DomainName|DomainKey).</summary>
    public string BasicAuthHeader =>
        Convert.ToBase64String(
            System.Text.Encoding.UTF8.GetBytes($"{DomainName}|{DomainKey}"));
}
