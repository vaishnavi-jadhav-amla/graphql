namespace BStore.GraphQL.Api.GraphQL.Types;

// ── B-store list ───────────────────────────────────────────────────────────────

/// <summary>A single B-store portal in a list response.</summary>
public sealed class BStoreListItem
{
    public int    PortalId      { get; set; }
    public string StoreName     { get; set; } = "";
    public string? DomainUrl    { get; set; }
    public bool   IsActive      { get; set; }
    public string? StoreCode    { get; set; }
    public string? CreatedDate  { get; set; }
    public string? ModifiedDate { get; set; }
    public int?   MediaId       { get; set; }
    public string? LogoUrl      { get; set; }
}

/// <summary>Top-level result of the B-store list query.</summary>
public sealed class BStoreListResult
{
    public List<BStoreListItem> BStores       { get; set; } = [];
    public bool                 IsBStoreManager { get; set; }
    public bool                 IsBStoreOwner   { get; set; }
}

// ── B-store details ────────────────────────────────────────────────────────────

/// <summary>Full portal configuration returned by GET /v2/b-stores/{storeId}.</summary>
public sealed class BStoreDetails
{
    public int     PortalId              { get; set; }
    public string  StoreName             { get; set; } = "";
    public string? DomainURL             { get; set; }
    public bool    IsActive              { get; set; }
    public string? StoreCode             { get; set; }
    public int?    ParentPortalId        { get; set; }
    public string? CustomCSSClass        { get; set; }
    public string? GlobalAttributes      { get; set; }
    public string? LocaleCode            { get; set; }
    public string? CurrencyCode          { get; set; }
    public string? TimeZone              { get; set; }
    public string? WebsiteTitle          { get; set; }
    public bool    EnableGuestUserSignup  { get; set; }
    public bool    EnableUserRegistration { get; set; }
    public string? CSSThemeName          { get; set; }
    public string? ExternalId            { get; set; }
}

// ── B-store design / theme ─────────────────────────────────────────────────────

/// <summary>Theme/branding configuration returned by GET /v2/b-stores/{storeId}/theme.</summary>
public sealed class BStoreDesign
{
    public string? WebsiteTitle  { get; set; }
    public int?    MediaId       { get; set; }
    public string? FaviconPath   { get; set; }
    public int?    FaviconMediaId{ get; set; }
    public string? LogoPath      { get; set; }
    public string? CSSThemeName  { get; set; }
    public string? CustomCSS     { get; set; }
    public string? PrimaryColor  { get; set; }
    public string? SecondaryColor{ get; set; }
}

// ── Catalogs ───────────────────────────────────────────────────────────────────

public sealed class CatalogItem
{
    public int    PublishCatalogId { get; set; }
    public string CatalogName      { get; set; } = "";
    public string? CatalogCode     { get; set; }
    public bool   IsAssociated     { get; set; }
}

// ── Price lists ────────────────────────────────────────────────────────────────

public sealed class PriceListItem
{
    public int    PriceListId   { get; set; }
    public string PriceListName { get; set; } = "";
    public string? PriceListCode{ get; set; }
    public bool   IsAssociated  { get; set; }
}

// ── Domains ────────────────────────────────────────────────────────────────────

public sealed class DomainListItem
{
    public int    DomainId   { get; set; }
    public string DomainName { get; set; } = "";
    public bool   IsActive   { get; set; }
}

// ── File upload ────────────────────────────────────────────────────────────────

public sealed class FileUploadResult
{
    public bool    HasError     { get; set; }
    public string? ErrorMessage { get; set; }
    public int?    MediaId      { get; set; }
    public string? FileName     { get; set; }
    public string? FilePath     { get; set; }
}

// ── Mutation results ───────────────────────────────────────────────────────────

public sealed class CreateStoreResult
{
    public bool   HasError     { get; set; }
    public string? ErrorMessage { get; set; }
    public int?   PortalId     { get; set; }
    public string? StoreName   { get; set; }
    public string? DomainURL   { get; set; }
}

public sealed class MutationResult
{
    public bool   HasError     { get; set; }
    public string? ErrorMessage { get; set; }
    public bool   IsSuccess    { get; set; }
}

// ── Input types ────────────────────────────────────────────────────────────────

/// <summary>Input for creating a new B-store under a parent portal.</summary>
public sealed class CreateBStoreInput
{
    public string  StoreName       { get; set; } = "";
    public string? DomainURL       { get; set; }
    public string? StoreCode       { get; set; }
    public string? LocaleCode      { get; set; }
    public string? CurrencyCode    { get; set; }
    public bool    IsActive        { get; set; }
}

/// <summary>Input for duplicating an existing B-store (matches web <c>duplicateStore</c> payload).</summary>
public sealed class DuplicateBStoreInput
{
    public string  StoreName   { get; set; } = "";
    /// <summary>Full B-store domain value sent to the API as <c>BStoreDomainName</c>.</summary>
    public string? BStoreDomainName { get; set; }
    /// <summary>Legacy alias; when <see cref="BStoreDomainName"/> is null, this value is sent instead.</summary>
    public string? DomainURL   { get; set; }
    public string? StoreCode   { get; set; }
    public bool    IsBStoreAutoPublished { get; set; }
}

/// <summary>Input for updating B-store theme/branding.</summary>
public sealed class UpdateBStoreDesignInput
{
    public string? WebsiteTitle   { get; set; }
    public int?    MediaId        { get; set; }
    public int?    FaviconMediaId { get; set; }
    public string? CSSThemeName   { get; set; }
    public string? CustomCSS      { get; set; }
    public string? PrimaryColor   { get; set; }
    public string? SecondaryColor { get; set; }
}

/// <summary>Input for updating B-store core settings.</summary>
public sealed class UpdateBStoreSettingsInput
{
    public string? StoreName              { get; set; }
    public string? DomainURL              { get; set; }
    public bool?   IsActive               { get; set; }
    public string? LocaleCode             { get; set; }
    public string? CurrencyCode           { get; set; }
    public string? TimeZone               { get; set; }
    public bool?   EnableGuestUserSignup  { get; set; }
    public bool?   EnableUserRegistration { get; set; }
    public string? ExternalId             { get; set; }
}

// ── Auth / token ───────────────────────────────────────────────────────────────

/// <summary>Result of a B-store token validation request.</summary>
public sealed class BStoreValidateTokenResult
{
    public bool                 HasError      { get; set; }
    public string?              ErrorMessage  { get; set; }
    /// <summary>Populated when <see cref="HasError"/> is <c>false</c>.</summary>
    public BStoreAccessDetails? AccessDetails { get; set; }
}

/// <summary>Impersonation / SSO details returned on a successful token validation.</summary>
public sealed class BStoreAccessDetails
{
    public string  Token           { get; set; } = "";
    public string? FirstName       { get; set; }
    public int?    WebstoreUserId  { get; set; }
    public int?    PortalId        { get; set; }
    public string? ParentStoreName { get; set; }
}

// ── User input ─────────────────────────────────────────────────────────────────

/// <summary>Input for updating a user's core profile fields.</summary>
public sealed class UserUpdateInput
{
    public int     UserId      { get; set; }
    public string? FirstName   { get; set; }
    public string? LastName    { get; set; }
    public string? Email       { get; set; }
    public string? PhoneNumber { get; set; }
    public string? ExternalId  { get; set; }
}
