using BStore.GraphQL.Api.Configuration;
using Microsoft.Extensions.Options;

namespace BStore.GraphQL.Api.Media;

/// <summary>
/// ADR-014: rewrites a stored relative media path to a CDN-hosted absolute URL. Never proxies binaries.
/// </summary>
public sealed class MediaUrlBuilder(IOptions<MediaOptions> options)
{
    private readonly MediaOptions _opts = options.Value;

    public string? ToCdnUrl(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;
        if (path.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            return path;

        if (string.IsNullOrWhiteSpace(_opts.CdnBaseUrl))
            return path;

        var cdn    = _opts.CdnBaseUrl.TrimEnd('/');
        var prefix = string.IsNullOrWhiteSpace(_opts.CdnPathPrefix) ? "" : "/" + _opts.CdnPathPrefix.Trim('/');
        var rel    = path.TrimStart('/');
        return $"{cdn}{prefix}/{rel}";
    }
}
