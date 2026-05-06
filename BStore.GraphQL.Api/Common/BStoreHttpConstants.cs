namespace BStore.GraphQL.Api.Common;

/// <summary>
/// Query-string values shared with <c>znode10-bstore-web</c> store-service defaults.
/// </summary>
public static class BStoreHttpConstants
{
    /// <summary>Filter catalogs to rows managed in B-Store (matches web <c>CATALOG_FILTERS.MANAGED_IN_BSTORE</c>).</summary>
    public const string CatalogFilterManagedInBStore = "ismanageinbstore~eq~1";
}
