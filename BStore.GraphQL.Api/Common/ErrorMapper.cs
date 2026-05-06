using BStore.GraphQL.Api.Diagnostics;
using HotChocolate;

namespace BStore.GraphQL.Api.Common;

/// <summary>
/// Centralised conversion from exceptions to typed GraphQL errors with the ADR-019 envelope:
/// <c>{ message, extensions: { code, category, correlationId, details, timestamp } }</c>.
/// </summary>
public static class ErrorMapper
{
    public static GraphQLException ToGraphQL(Exception ex) =>
        ToGraphQL(ex, ErrorCodes.BStoreError, null);

    public static GraphQLException ToGraphQL(Exception ex, string code) =>
        ToGraphQL(ex, code, null);

    public static GraphQLException ToGraphQL(Exception ex, string code, IRequestDebugContext? debug)
    {
        var category = code switch
        {
            ErrorCodes.Validation       => ErrorCategory.Validation,
            ErrorCodes.NotFound         => ErrorCategory.NotFound,
            ErrorCodes.Upload           => ErrorCategory.Provider,
            ErrorCodes.AdminRequired    => ErrorCategory.Authorization,
            ErrorCodes.PageSizeExceeded => ErrorCategory.Validation,
            ErrorCodes.DepthExceeded    => ErrorCategory.Validation,
            _                           => ErrorCategory.Internal
        };

        var builder = ErrorBuilder.New()
            .SetMessage(ex.Message)
            .SetCode(code)
            .SetExtension("code", code)
            .SetExtension("category", category)
            .SetExtension("source", "Database")
            .SetExtension("timestamp", DateTimeOffset.UtcNow.ToString("o"));

        if (!string.IsNullOrWhiteSpace(debug?.CorrelationId))
            builder.SetExtension("correlationId", debug!.CorrelationId);

        return new GraphQLException(builder.Build());
    }
}
