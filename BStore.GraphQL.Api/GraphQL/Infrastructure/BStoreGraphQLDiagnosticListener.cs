using System.Diagnostics;
using BStore.GraphQL.Api.Configuration;
using BStore.GraphQL.Api.Diagnostics;
using HotChocolate.Execution;
using HotChocolate.Execution.Instrumentation;
using HotChocolate.Resolvers;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace BStore.GraphQL.Api.GraphQL.Infrastructure;

/// <summary>
/// Emits structured logs and per-request <c>extensions</c> diagnostics
/// (ADR-018/022/023/024/025/027). Detail is gated by <see cref="DebugLevel"/> after admin-token check.
/// </summary>
public sealed class BStoreGraphQLDiagnosticListener(
    ILogger<BStoreGraphQLDiagnosticListener> logger,
    IOptions<GraphQLOptions> options,
    IHttpContextAccessor httpAccessor,
    IProviderHealthTracker providerHealth) : ExecutionDiagnosticEventListener
{
    private readonly int _slowMs = Math.Max(1, options.Value.SlowQueryThresholdMs);

    public override IDisposable ExecuteRequest(IRequestContext context)
    {
        var sw = Stopwatch.StartNew();
        var debug = httpAccessor.HttpContext?.RequestServices.GetService<IRequestDebugContext>();
        var correlationId = debug?.CorrelationId ?? Guid.NewGuid().ToString("N");
        if (debug is not null) debug.OperationName = context.Request.OperationName ?? "anonymous";

        logger.LogInformation(
            "GraphQL request started | CorrelationId={CorrelationId} | Operation={Operation} | UserId={UserId} | ClientId={ClientId}",
            correlationId,
            context.Request.OperationName ?? "anonymous",
            debug?.UserId ?? 0,
            debug?.ClientId ?? "n/a");

        return new RequestScope(context, sw, correlationId, logger, _slowMs, debug, providerHealth);
    }

    public override void ResolverError(IMiddlewareContext context, IError error)
    {
        var debug = httpAccessor.HttpContext?.RequestServices.GetService<IRequestDebugContext>();
        logger.LogError(
            "Resolver error | CorrelationId={CorrelationId} | Field={Field} | Type={Type} | {Message} | Code={Code}",
            debug?.CorrelationId ?? "n/a",
            context.Selection.Field.Name,
            context.Selection.Type.NamedType().Name,
            error.Message,
            error.Code);
    }

    public override void RequestError(IRequestContext context, Exception exception)
    {
        var debug = httpAccessor.HttpContext?.RequestServices.GetService<IRequestDebugContext>();
        logger.LogError(exception,
            "GraphQL request error | CorrelationId={CorrelationId} | Operation={Operation}",
            debug?.CorrelationId ?? "n/a",
            context.Request.OperationName ?? "anonymous");
    }

    private sealed class RequestScope(
        IRequestContext request,
        Stopwatch stopwatch,
        string correlationId,
        ILogger logger,
        int slowThresholdMs,
        IRequestDebugContext? debug,
        IProviderHealthTracker providerHealth) : IDisposable
    {
        public void Dispose()
        {
            stopwatch.Stop();
            var ms = stopwatch.ElapsedMilliseconds;

            EmitExtensions(ms);

            if (ms > slowThresholdMs)
            {
                logger.LogWarning(
                    "SLOW GraphQL request | CorrelationId={CorrelationId} | Operation={Operation} | Duration={Ms}ms (threshold {Threshold}ms)",
                    correlationId, request.Request.OperationName ?? "anonymous", ms, slowThresholdMs);
            }
            else
            {
                logger.LogInformation(
                    "GraphQL request completed | CorrelationId={CorrelationId} | Operation={Operation} | Duration={Ms}ms",
                    correlationId, request.Request.OperationName ?? "anonymous", ms);
            }
        }

        private void EmitExtensions(long totalMs)
        {
            if (debug is null) return;

            var snap = debug.Snapshot();
            var ext = new Dictionary<string, object?>
            {
                ["correlationId"] = correlationId,
                ["totalMs"]       = totalMs
            };

            if (snap.EmptyReasons.Count > 0)
                ext["emptyResultReasons"] = snap.EmptyReasons.Select(r => new { code = r.Code, message = r.Message });

            if (snap.Level >= DebugLevel.Detailed)
            {
                ext["timings"]      = snap.Timings.Select(t => new { stage = t.Stage, ms = t.ElapsedMs });
                ext["dataSources"]  = snap.DataSources;
                ext["pipeline"]     = snap.PipelineNotes.Select(n => new { step = n.Step, details = n.Details, at = n.At });
                ext["providers"]    = providerHealth.Snapshot();
                ext["operation"]    = snap.OperationName;
                ext["userId"]       = snap.UserId;
                ext["clientId"]     = snap.ClientId;
            }

            try
            {
                if (request.Result is IOperationResult op)
                {
                    var merged = op.ContextData is null
                        ? new Dictionary<string, object?>()
                        : new Dictionary<string, object?>(op.ContextData!);
                    foreach (var kv in ext) merged[kv.Key] = kv.Value;
                    request.Result = OperationResultBuilder.FromResult(op).SetExtensions(ext).Build();
                }
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "Failed to enrich GraphQL extensions");
            }
        }
    }
}
