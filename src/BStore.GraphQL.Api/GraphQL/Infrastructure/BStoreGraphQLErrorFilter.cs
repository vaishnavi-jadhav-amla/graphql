using BStore.GraphQL.Api.Common;
using BStore.GraphQL.Api.Diagnostics;
using HotChocolate;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace BStore.GraphQL.Api.GraphQL.Infrastructure;

/// <summary>
/// Maps exceptions to the ADR-019 structured envelope with stable
/// <c>code</c>, <c>category</c>, <c>correlationId</c>, <c>details</c>, <c>timestamp</c>, and <c>operation</c>.
/// </summary>
public sealed class BStoreGraphQLErrorFilter(
    ILogger<BStoreGraphQLErrorFilter> logger,
    IHttpContextAccessor httpAccessor) : IErrorFilter
{
    public IError OnError(IError error)
    {
        if (error.Exception is GraphQLException)
            return AugmentEnvelope(error);

        var ex = error.Exception;
        if (ex is OperationCanceledException)
            return error;

        var debug = httpAccessor.HttpContext?.RequestServices.GetService(typeof(IRequestDebugContext)) as IRequestDebugContext;
        var corr  = debug?.CorrelationId ?? "n/a";

        if (ex is not null)
        {
            logger.LogError(ex,
                "GraphQL error | CorrelationId={CorrelationId} | Path={Path} | {ExType}: {Message}",
                corr,
                error.Path?.ToString() ?? "unknown",
                ex.GetType().Name,
                ex.Message);
        }

        var realMessage = ex?.Message ?? error.Message;

        (string message, string code, string category, string? details) = ex switch
        {
            ArgumentException arg => (
                arg.Message,
                "INVALID_ARGUMENT",
                ErrorCategory.Validation,
                "The input provided is invalid. Check field arguments and try again."),

            System.Collections.Generic.KeyNotFoundException knf => (
                knf.Message,
                "NOT_FOUND",
                ErrorCategory.NotFound,
                "The requested resource was not found."),

            UnauthorizedAccessException ua => (
                string.IsNullOrWhiteSpace(ua.Message) || ua.Message.Contains("UnauthorizedAccessException", StringComparison.Ordinal)
                    ? "You are not authorized to perform this action."
                    : ua.Message,
                "UNAUTHORIZED",
                ErrorCategory.Authorization,
                "Authentication may be required for this operation."),

            InvalidOperationException io => (
                io.Message,
                io.Message.Contains("Rate limit", StringComparison.OrdinalIgnoreCase)
                    ? "RATE_LIMITED"
                    : "INVALID_OPERATION",
                io.Message.Contains("Rate limit", StringComparison.OrdinalIgnoreCase)
                    ? ErrorCategory.RateLimited
                    : ErrorCategory.Internal,
                io.Message.Contains("Rate limit", StringComparison.OrdinalIgnoreCase)
                    ? "Too many requests. Retry after a short wait."
                    : "The operation could not be completed in its current state."),

            TimeoutException => (
                "The operation timed out. Please try again later.",
                "TIMEOUT",
                ErrorCategory.Timeout,
                "The server took too long to respond."),

            DbUpdateException db => (
                "A database error occurred while processing your request.",
                "DATABASE_ERROR",
                ErrorCategory.Database,
                TruncateDetail(db.InnerException?.Message ?? db.Message)),

            _ => (
                ex is not null
                    ? $"An error occurred while processing your request: {SanitizeMessage(realMessage)}"
                    : error.Message,
                error.Code ?? "INTERNAL_ERROR",
                ErrorCategory.Internal,
                ex is not null
                    ? "An unexpected error occurred. If it persists, contact support with the operation name and time."
                    : null)
        };

        var builder = ErrorBuilder.FromError(error)
            .SetMessage(message)
            .SetCode(code)
            .RemoveExtension("stackTrace")
            .SetExtension("code", code)
            .SetExtension("category", category)
            .SetExtension("correlationId", corr)
            .SetExtension("timestamp", DateTime.UtcNow.ToString("o"));

        if (details is not null)
            builder.SetExtension("details", details);

        if (error.Path is not null)
            builder.SetExtension("operation", error.Path.ToString());

        return builder.Build();
    }

    /// <summary>Adds correlationId / category to errors thrown explicitly by resolvers.</summary>
    private IError AugmentEnvelope(IError error)
    {
        var debug = httpAccessor.HttpContext?.RequestServices.GetService(typeof(IRequestDebugContext)) as IRequestDebugContext;
        var corr  = debug?.CorrelationId;

        var builder = ErrorBuilder.FromError(error);
        if (!string.IsNullOrWhiteSpace(corr) && !error.Extensions!.ContainsKey("correlationId"))
            builder.SetExtension("correlationId", corr);

        if (!error.Extensions!.ContainsKey("timestamp"))
            builder.SetExtension("timestamp", DateTime.UtcNow.ToString("o"));

        if (!error.Extensions!.ContainsKey("category") && !string.IsNullOrEmpty(error.Code))
        {
            var cat = error.Code switch
            {
                "BSTORE_VALIDATION" or "INVALID_ARGUMENT" or "BSTORE_PAGE_SIZE_EXCEEDED" or "BSTORE_DEPTH_EXCEEDED" => ErrorCategory.Validation,
                "BSTORE_NOT_FOUND" or "NOT_FOUND" => ErrorCategory.NotFound,
                "UNAUTHORIZED" => ErrorCategory.Authorization,
                "BSTORE_ADMIN_REQUIRED" => ErrorCategory.Authorization,
                "TIMEOUT" => ErrorCategory.Timeout,
                "DATABASE_ERROR" => ErrorCategory.Database,
                "RATE_LIMITED" => ErrorCategory.RateLimited,
                "BSTORE_PROVIDER_UNAVAILABLE" or "BSTORE_UPLOAD_ERROR" => ErrorCategory.Provider,
                _ => ErrorCategory.Internal
            };
            builder.SetExtension("category", cat);
        }

        return builder.Build();
    }

    private static string? TruncateDetail(string? s) =>
        string.IsNullOrEmpty(s) ? null : (s.Length > 400 ? s[..400] + "…" : s);

    private static string SanitizeMessage(string message)
    {
        if (message.Contains("connection string", StringComparison.OrdinalIgnoreCase)
            || message.Contains("SqlException", StringComparison.OrdinalIgnoreCase)
            || message.Contains("login failed", StringComparison.OrdinalIgnoreCase))
            return "A database connection error occurred.";

        return message.Length > 300 ? message[..300] + "…" : message;
    }
}
