namespace BStore.GraphQL.Api.Common;

/// <summary>Standardized GraphQL error codes used in all B-store error responses (ADR-019).</summary>
public static class ErrorCodes
{
    public const string BStoreError              = "BSTORE_ERROR";
    public const string NotFound                 = "BSTORE_NOT_FOUND";
    public const string Validation               = "BSTORE_VALIDATION";
    public const string Upload                   = "BSTORE_UPLOAD_ERROR";
    public const string NoHttpContext            = "BSTORE_NO_HTTP_CONTEXT";
    public const string NotSupportedDbOperation  = "BSTORE_NOT_SUPPORTED_DB";
    public const string AdminRequired            = "BSTORE_ADMIN_REQUIRED";
    public const string PageSizeExceeded         = "BSTORE_PAGE_SIZE_EXCEEDED";
    public const string DepthExceeded            = "BSTORE_DEPTH_EXCEEDED";
    public const string ProviderUnavailable      = "BSTORE_PROVIDER_UNAVAILABLE";
    public const string FieldAccessDenied        = "FIELD_ACCESS_DENIED";
}
