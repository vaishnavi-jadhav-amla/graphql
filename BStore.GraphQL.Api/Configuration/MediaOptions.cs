namespace BStore.GraphQL.Api.Configuration;

/// <summary>ADR-014: storefront media is served from a CDN — never proxied through the GraphQL API.</summary>
public sealed class MediaOptions
{
    public const string Section = "Media";

    /// <summary>CDN base URL, e.g. <c>https://cdn.bstore.example</c>. Empty disables rewriting.</summary>
    public string CdnBaseUrl { get; init; } = "";

    /// <summary>Optional path prefix below the CDN host (e.g. <c>/media/</c>).</summary>
    public string CdnPathPrefix { get; init; } = "";

    /// <summary>Local filesystem folder where uploaded files are written. Created on demand.</summary>
    public string LocalUploadFolder { get; init; } = "App_Data/Uploads";

    /// <summary>Public-facing relative URL prefix used to compose returned <c>FilePath</c>.</summary>
    public string LocalUploadUrlPrefix { get; init; } = "/media";

    /// <summary>Per-file size cap (bytes). Default: 10 MB.</summary>
    public long MaxUploadBytes { get; init; } = 10L * 1024 * 1024;
}
