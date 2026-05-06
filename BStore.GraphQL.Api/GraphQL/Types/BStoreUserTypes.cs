namespace BStore.GraphQL.Api.GraphQL.Types;

// ──────────────────────────────────────────────────────────────────────────────
// DTOs for IBStoresUserService operations.
// These are flat GraphQL output types / input objects for B-store user access
// and role management, decoupled from the Znode model graph.
// ──────────────────────────────────────────────────────────────────────────────

/// <summary>Role access flags for a user within the B-store system.</summary>
public sealed class BStoreUserRoleRow
{
    public int UserId { get; set; }
    public bool IsManager { get; set; }
    public bool IsOwner { get; set; }
}

/// <summary>A single B-store entry in a user's access list (associated or unassociated).</summary>
public sealed class BStoreUserAccessRow
{
    public int PortalId { get; set; }
    public string StoreName { get; set; } = "";
    public bool IsActive { get; set; }
    public bool IsAssociated { get; set; }
}

/// <summary>Paginated result for <see cref="BStoreUserAccessRow"/>.</summary>
public sealed class BStoreUserAccessListResult
{
    public List<BStoreUserAccessRow> Items { get; set; } = [];
    public int TotalCount { get; set; }
    public int PageIndex { get; set; }
    public int PageSize { get; set; }
}

// ── Input types ───────────────────────────────────────────────────────────────

/// <summary>Input for saving a user's B-store role (manager / owner flags).</summary>
public sealed class BStoreUserRoleInput
{
    public int UserId { get; set; }
    public bool IsManager { get; set; }
    public bool IsOwner { get; set; }
}

/// <summary>Input for associating or unassociating a user with a set of B-store portals.</summary>
public sealed class BStoreUserAccessInput
{
    public int UserId { get; set; }

    /// <summary><c>true</c> to associate; <c>false</c> to unassociate.</summary>
    public bool IsAssociate { get; set; }

    public List<int> PortalIds { get; set; } = [];
}
