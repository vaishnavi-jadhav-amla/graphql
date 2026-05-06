namespace BStore.GraphQL.Api.Caching;

/// <summary>
/// Granularity of a <see cref="ICacheFlushService.FlushAsync"/> call. Twelve modes covering
/// global resets, layer-specific flushes, entity-family eviction, and single-entity invalidation.
/// </summary>
public enum CacheFlushScope
{
    /// <summary>Both L1 and L2 across every tracked key.</summary>
    All = 0,

    /// <summary>L1 only (per-instance memory). L2 entries remain — survived for warm-up.</summary>
    L1Only = 1,

    /// <summary>L2 only (Redis / distributed). L1 stays for the duration of its TTL.</summary>
    L2Only = 2,

    /// <summary>Every <c>bstore:*</c> key.</summary>
    BStores = 3,

    /// <summary>Single B-store (<c>EntityId</c> = storeId): identity + theme.</summary>
    BStore = 4,

    /// <summary>Every <c>user:*</c> key.</summary>
    Users = 5,

    /// <summary>Single user (<c>EntityId</c> = userId): identity + role/access.</summary>
    User = 6,

    /// <summary>Every <c>product:*</c> key.</summary>
    Products = 7,

    /// <summary>Single product (<c>EntityId</c> = productId): all sub-keys (base, price, inventory, seo, attr).</summary>
    Product = 8,

    /// <summary>Category trees (<c>catalog:*:tree:*</c>).</summary>
    Categories = 9,

    /// <summary>Global attribute group definitions (<c>attr:groups:*</c>).</summary>
    Attributes = 10,

    /// <summary>Portal-scoped slice (<c>EntityId</c> = portalId): bstore, catalogs, pricelists, attribute groups, domain suffix.</summary>
    Portal = 11
}
