namespace BStore.GraphQL.Api.Caching;

/// <summary>
/// Deterministic, human-readable cache key builders for every GraphQL read query.
/// All B-store keys share the <c>bstore:</c> prefix; user keys use <c>user:</c>;
/// product keys use <c>product:</c> — enabling targeted bulk eviction.
/// </summary>
internal static class CacheKeys
{
    // ── B-store keys ──────────────────────────────────────────────────────

    private const string BStorePrefix = "bstore";

    internal static string BStoreList(int portalId, int userId, int pageIndex, int pageSize) =>
        $"{BStorePrefix}:list:{portalId}:{userId}:{pageIndex}:{pageSize}";

    internal static string BStore(int storeId) =>
        $"{BStorePrefix}:portal:{storeId}";

    internal static string BStoreTheme(int storeId) =>
        $"{BStorePrefix}:theme:{storeId}";

    internal static string BStoreCatalogs(int portalId, bool associated, int pageIndex, int pageSize, string? filter) =>
        $"{BStorePrefix}:catalogs:{portalId}:{associated}:{pageIndex}:{pageSize}:{filter ?? ""}";

    internal static string BStorePriceLists(int portalId, bool associated, int pageIndex, int pageSize) =>
        $"{BStorePrefix}:pricelists:{portalId}:{associated}:{pageIndex}:{pageSize}";

    internal static string BStoreDomainNameSuffix(int portalId) =>
        $"{BStorePrefix}:domain-suffix:{portalId}";

    internal static string DomainList(int pageIndex, int pageSize) =>
        $"{BStorePrefix}:domains:{pageIndex}:{pageSize}";

    /// <summary>
    /// Keys evicted when a B-store portal's core data changes (update, activation toggle, copy).
    /// BStoreList keys expire naturally via TTL — combinatorial key space makes exhaustive invalidation impractical.
    /// </summary>
    internal static IEnumerable<string> ForBStore(int storeId) =>
        [BStore(storeId), BStoreTheme(storeId)];

    // ── B-store user access keys ──────────────────────────────────────────

    internal static string BStoreUserRoleAccess(int userId) =>
        $"{BStorePrefix}:user-role:{userId}";

    internal static string BStoreUserAccessList(int userId, bool isAssociated, int pageIndex, int pageSize) =>
        $"{BStorePrefix}:user-access:{userId}:{isAssociated}:{pageIndex}:{pageSize}";

    internal static IEnumerable<string> ForBStoreUser(int userId) =>
        [BStoreUserRoleAccess(userId)];

    // ── User keys ─────────────────────────────────────────────────────────

    private const string UserPrefix = "user";

    internal static string User(int userId) =>
        $"{UserPrefix}:{userId}";

    internal static string UserByUsername(string username, string storeCode) =>
        $"{UserPrefix}:username:{username}:{storeCode}";

    internal static IEnumerable<string> ForUser(int userId) =>
        [User(userId)];

    // ── Product keys ──────────────────────────────────────────────────────
    // ADR-002: never store one fat blob per product; cache each component independently
    // so price/inventory can have their own TTL (ADR-016) without churning the rest.

    private const string ProductPrefix = "product";

    internal static string ProductList(int limit, int skip, string? sortBy, string? order, string? select) =>
        $"{ProductPrefix}:list:{limit}:{skip}:{sortBy}:{order}:{select ?? ""}";

    internal static string Product(int id) =>
        $"{ProductPrefix}:{id}";

    internal static string ProductSearch(string q, int limit, int skip) =>
        $"{ProductPrefix}:search:{q}:{limit}:{skip}";

    internal static string ProductCategories() =>
        $"{ProductPrefix}:categories";

    internal static string ProductsByCategory(string category, int limit, int skip) =>
        $"{ProductPrefix}:category:{category}:{limit}:{skip}";

    /// <summary>Static product detail (name, description, brand, images). Long TTL.</summary>
    internal static string ProductBase(int id)       => $"{ProductPrefix}:{id}:base";

    /// <summary>Pricing — must use <c>InventoryPricing</c> TTL (ADR-016 ≤ 30s).</summary>
    internal static string ProductPrice(int id)      => $"{ProductPrefix}:{id}:price";

    /// <summary>Inventory / stock — must use <c>InventoryPricing</c> TTL (ADR-016 ≤ 30s).</summary>
    internal static string ProductInventory(int id)  => $"{ProductPrefix}:{id}:inventory";

    /// <summary>SEO meta — Lookup TTL.</summary>
    internal static string ProductSeo(int id)        => $"{ProductPrefix}:{id}:seo";

    /// <summary>Attribute-based fields per group (ADR-001) — Lookup TTL.</summary>
    internal static string ProductAttributes(int id, string group) => $"{ProductPrefix}:{id}:attr:{group}";

    /// <summary>Storefront entry-point cache (ADR-008).</summary>
    internal static string WebsiteEntry(string portal, string locale, string path) =>
        $"website:entry:{portal}:{locale}:{path}";

    /// <summary>Category tree (ADR-015 materialized path).</summary>
    internal static string CategoryTree(int catalogId, string locale) =>
        $"catalog:{catalogId}:tree:{locale}";

    /// <summary>Global attribute group definitions (ADR-001).</summary>
    internal static string GlobalAttributeGroups(int portalId, string locale) =>
        $"attr:groups:{portalId}:{locale}";
}
