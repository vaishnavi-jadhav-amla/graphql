namespace BStore.GraphQL.Api.Common;

/// <summary>Error envelope category (ADR-019). Stable strings exposed under <c>extensions.category</c>.</summary>
public static class ErrorCategory
{
    public const string Validation     = "VALIDATION";
    public const string Authentication = "AUTHENTICATION";
    public const string Authorization  = "AUTHORIZATION";
    public const string NotFound       = "NOT_FOUND";
    public const string Conflict       = "CONFLICT";
    public const string RateLimited    = "RATE_LIMITED";
    public const string Database       = "DATABASE";
    public const string Provider       = "PROVIDER";
    public const string Timeout        = "TIMEOUT";
    public const string Internal       = "INTERNAL";
}
