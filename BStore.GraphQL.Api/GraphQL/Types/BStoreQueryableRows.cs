namespace BStore.GraphQL.Api.GraphQL.Types;

/// <summary>Flat row for GraphQL filtering/sorting over B-store portals (EF / <c>ZnodePortals</c>).</summary>
public sealed class BStorePortalQueryableRow
{
    public int     PortalId        { get; set; }
    public int?    ParentPortalId  { get; set; }
    public string   StoreName      { get; set; } = "";
    public string?  StoreCode      { get; set; }
    public bool     IsActive       { get; set; }
    public bool     IsBStore       { get; set; }
    public string?  BStoreDomainName { get; set; }
}

/// <summary>Flat row for B-store catalog assignments (<c>ZnodeBStoresAvailableCatalogs</c> + catalog).</summary>
public sealed class BStoreCatalogAssignmentQueryableRow
{
    public int    BStoresAvailableCatalogId { get; set; }
    public int    PortalId                  { get; set; }
    public int    PublishCatalogId          { get; set; }
    public string  CatalogName               { get; set; } = "";
    public string? CatalogCode              { get; set; }
    public bool   IsManageInBStore          { get; set; }
    public bool   IsDefaultCatalog         { get; set; }
}

/// <summary>Flat row for B-store price-list assignments.</summary>
public sealed class BStorePriceListAssignmentQueryableRow
{
    public int    BStoresAvailablePriceListId { get; set; }
    public int    PortalId                    { get; set; }
    public int    PriceListId                 { get; set; }
    public string ListCode                  { get; set; } = "";
    public string? ListName                { get; set; }
    public bool   IsManageInBStore          { get; set; }
    public bool   IsDefaultPriceList       { get; set; }
}

/// <summary>Flat row for <c>ZnodeDomains</c>.</summary>
public sealed class DomainQueryableRow
{
    public int     DomainId        { get; set; }
    public int     PortalId       { get; set; }
    public string  DomainName     { get; set; } = "";
    public bool    IsActive       { get; set; }
    public bool    IsDefault      { get; set; }
    public string? ApplicationType { get; set; }
}
