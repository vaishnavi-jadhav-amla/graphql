namespace BStore.GraphQL.Api.Diagnostics;

/// <summary>Stable codes for empty-result reasons surfaced to callers and admin tooling (ADR-020).</summary>
public static class EmptyResultReasons
{
    public const string PortalNotFound       = "PORTAL_NOT_FOUND";
    public const string PortalInactive       = "PORTAL_INACTIVE";
    public const string UserNotAssociated    = "USER_NOT_ASSOCIATED";
    public const string LocaleNotConfigured  = "LOCALE_NOT_CONFIGURED";
    public const string NoPublishedVersion   = "NO_PUBLISHED_VERSION";
    public const string FilterExcludedAll    = "FILTER_EXCLUDED_ALL";
    public const string CategoryNotFound     = "CATEGORY_NOT_FOUND";
    public const string CatalogNotAssigned   = "CATALOG_NOT_ASSIGNED";
    public const string SearchTermTooShort   = "SEARCH_TERM_TOO_SHORT";
    public const string IndexNotPopulated    = "INDEX_NOT_POPULATED";
}
