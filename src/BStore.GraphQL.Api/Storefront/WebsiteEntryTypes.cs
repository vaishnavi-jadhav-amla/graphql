using HotChocolate;

namespace BStore.GraphQL.Api.Storefront;

/// <summary>
/// Single entry-point storefront response (ADR-008) returned by <c>websiteEntry(portalCode, locale, path)</c>.
/// Aggregates theme, navigation breadcrumbs, page meta, and SEO so the client only issues one query
/// when first hitting any storefront page.
/// </summary>
public sealed class WebsiteEntry
{
    public int PortalId { get; init; }
    public string PortalCode { get; init; } = "";
    public string Locale { get; init; } = "";
    public WebsiteEntryTheme? Theme { get; init; }

    [GraphQLName("seo")]
    public WebsiteEntrySeo? Seo { get; init; }

    [GraphQLName("url")]
    public string CanonicalUrl { get; init; } = "";

    public string? PageType { get; init; }
}

public sealed class WebsiteEntryTheme
{
    public string? ThemeName { get; init; }
    public string? LogoUrl { get; init; }
    public string? FaviconUrl { get; init; }
    public string? PrimaryColor { get; init; }
    public string? SecondaryColor { get; init; }
}

public sealed class WebsiteEntrySeo
{
    public string? Title { get; init; }
    public string? Description { get; init; }
    public string? Keywords { get; init; }

    [GraphQLName("url")]
    public string? Url { get; init; }
}
