using BStore.GraphQL.Api.Auth;
using BStore.GraphQL.Api.Auth.FieldPermissions;
using HotChocolate;

namespace BStore.GraphQL.Api.GraphQL.Types;

// ──────────────────────────────────────────────────────────────────────────────
// Product list/detail DTOs mapped from Znode publish tables (ZnodePublishProductDetail, etc.).
// ADR-006: acronyms (SKU, SEO, URL, ERP, PIM, OMS, B2B) keep their original casing in the schema.
// ──────────────────────────────────────────────────────────────────────────────

/// <summary>A single published product row (GraphQL projection).</summary>
public sealed class ProductRow
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string Category { get; set; } = "";
    public decimal Price { get; set; }

    [RequirePermission(Roles = [AuthConstants.RoleAdmin, AuthConstants.RoleBStoreOwner])]
    public double DiscountPercentage { get; set; }

    public double Rating { get; set; }

    [RequirePermission(Permissions = ["inventory.read"],
        Roles = [AuthConstants.RoleAdmin, AuthConstants.RoleBStoreOwner, AuthConstants.RoleBStoreEmployee])]
    public int Stock { get; set; }
    public string? Brand { get; set; }

    [GraphQLName("sku")]
    public string? Sku { get; set; }

    public string? Thumbnail { get; set; }
    public List<string> Tags { get; set; } = [];
    public List<string> Images { get; set; } = [];
}

/// <summary>Paginated product list result.</summary>
public sealed class ProductListResult
{
    public List<ProductRow> Products { get; set; } = [];
    public int Total { get; set; }
    public int Skip { get; set; }
    public int Limit { get; set; }
}

/// <summary>A product category entry.</summary>
public sealed class ProductCategoryRow
{
    public string Slug { get; set; } = "";
    public string Name { get; set; } = "";

    [GraphQLName("url")]
    public string Url { get; set; } = "";
}
