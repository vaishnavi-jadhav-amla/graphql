namespace BStore.GraphQL.Api.Configuration;

/// <summary>
/// Permission configuration. Controls which role checks are enforced.
/// Bound to the <c>Permissions</c> configuration section.
/// </summary>
public sealed class PermissionSettings
{
    public const string Section = "Permissions";

    /// <summary>If true, API key requests bypass all permission checks.</summary>
    public bool ApiKeyBypassAll { get; set; } = true;

    /// <summary>If true, Admin role bypasses all permission checks.</summary>
    public bool AdminRoleBypassAll { get; set; } = true;

    /// <summary>If true, frontend customer role bypasses DB permission checks.</summary>
    public bool FrontendRoleBypassAll { get; set; } = true;

    /// <summary>
    /// List of disabled permission check names.
    /// Use "*" to disable all checks (development only).
    /// </summary>
    public string[] DisabledChecks { get; set; } = [];
}
