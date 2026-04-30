using BStore.GraphQL.Api.Configuration;
using BStore.GraphQL.Api.Diagnostics;
using Microsoft.Extensions.Options;

namespace BStore.GraphQL.Api.Middleware;

/// <summary>
/// ADR-018: every HTTP request carries an <c>X-Correlation-ID</c> for distributed log correlation.
/// ADR-026: also seeds <see cref="IRequestDebugContext"/> with the correlation id, debug level header,
/// and admin-token gating so downstream resolvers and the diagnostic listener share one snapshot.
/// </summary>
public sealed class CorrelationIdMiddleware(
    RequestDelegate next,
    ILogger<CorrelationIdMiddleware> logger)
{
    public const string HeaderName = "X-Correlation-ID";
    public const string ClientIdHeader = "X-Client-Id";

    public Task Invoke(HttpContext context)
    {
        string id;
        if (!context.Request.Headers.TryGetValue(HeaderName, out var existing)
            || string.IsNullOrWhiteSpace(existing))
        {
            id = Guid.NewGuid().ToString("N");
            context.Request.Headers[HeaderName] = id;
            logger.LogDebug("Generated correlation ID: {CorrelationId}", id);
        }
        else
        {
            id = existing.ToString();
        }

        var debug = context.RequestServices.GetService<IRequestDebugContext>();
        if (debug is RequestDebugContext concrete)
        {
            concrete.ClientId = context.Request.Headers.TryGetValue(ClientIdHeader, out var clientId)
                ? clientId.ToString()
                : null;

            var opts = context.RequestServices.GetService<IOptions<GraphQLOptions>>()?.Value;
            ApplyDebugLevel(context, concrete, opts);
        }

        context.Response.OnStarting(_ =>
        {
            if (!context.Response.Headers.ContainsKey(HeaderName))
                context.Response.Headers.Append(HeaderName, id);
            return Task.CompletedTask;
        }, string.Empty);

        return next(context);
    }

    private static void ApplyDebugLevel(HttpContext context, RequestDebugContext debug, GraphQLOptions? opts)
    {
        var requested = context.Request.Headers.TryGetValue(opts?.DebugLevelHeader ?? "X-Debug-Level", out var hv)
            ? hv.ToString()
            : null;

        var hasAdmin = !string.IsNullOrWhiteSpace(opts?.AdminToken)
                       && context.Request.Headers.TryGetValue(opts.AdminTokenHeader, out var t)
                       && string.Equals(t.ToString(), opts.AdminToken, StringComparison.Ordinal);
        debug.IsAdmin = hasAdmin;

        var parsed = Enum.TryParse<DebugLevel>(requested, ignoreCase: true, out var p) ? p : DebugLevel.Basic;

        if (parsed == DebugLevel.Detailed && !hasAdmin)
            parsed = DebugLevel.Basic;

        debug.Level = parsed;
    }
}
