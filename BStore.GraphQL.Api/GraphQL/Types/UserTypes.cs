namespace BStore.GraphQL.Api.GraphQL.Types;

// ──────────────────────────────────────────────────────────────────────────────
// User DTOs mapped from <c>ZnodeUser</c> / AspNet identity via EF in this API.
// ──────────────────────────────────────────────────────────────────────────────

/// <summary>Paginated list of Znode users.</summary>
public sealed class UserListPage
{
    public List<UserRow> Users { get; set; } = [];
    public int           Total { get; set; }
    public int           Page { get; set; }
    public int           PageSize { get; set; }
}

/// <summary>Input for <c>POST /users</c> (web <c>ICreateUserPayload</c>).</summary>
public sealed class UserCreateInput
{
    public string  Name  { get; set; } = "";
    public string  Email { get; set; } = "";
    public string? Role  { get; set; }
}

/// <summary>Core user fields returned by user look-up queries.</summary>
public sealed class UserRow
{
    public int UserId { get; set; }
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string Email { get; set; } = "";
    public string? UserName { get; set; }
    public string? PhoneNumber { get; set; }
    public string? RoleName { get; set; }
    public bool IsActive { get; set; }
    public string? ExternalId { get; set; }
}

/// <summary>Detailed user information including store code context.</summary>
public sealed class UserDetailRow
{
    public int UserId { get; set; }
    public string UserName { get; set; } = "";
    public string Email { get; set; } = "";
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string? StoreCode { get; set; }
    public bool IsActive { get; set; }
    public string? RoleName { get; set; }
}
