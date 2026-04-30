namespace BStore.GraphQL.Api.Configuration;

/// <summary>ADR-014: storefront media is served from a CDN — never proxied through the GraphQL API.</summary>
public sealed class MediaOptions
{
    public const string Section = "Media";

    /// <summary>CDN base URL, e.g. <c>https://cdn.bstore.example</c>. Empty disables rewriting.</summary>
    public string CdnBaseUrl { get; init; } = "";

    /// <summary>Optional path prefix below the CDN host (e.g. <c>/media/</c>).</summary>
    public string CdnPathPrefix { get; init; } = "";
}
