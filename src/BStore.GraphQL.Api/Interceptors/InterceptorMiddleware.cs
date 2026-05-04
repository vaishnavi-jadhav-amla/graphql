using BStore.GraphQL.Api.Diagnostics;
using HotChocolate.Resolvers;

namespace BStore.GraphQL.Api.Interceptors;

/// <summary>
/// HotChocolate field middleware that executes registered <see cref="IBeforeAction"/>,
/// <see cref="ITransformResult"/>, and <see cref="IAfterAction"/> hooks around resolvers.
/// Registered interceptors are matched by operation name.
/// </summary>
public sealed class InterceptorMiddleware
{
    private readonly FieldDelegate _next;

    public InterceptorMiddleware(FieldDelegate next) => _next = next;

    public async Task InvokeAsync(IMiddlewareContext context)
    {
        var operationName = context.Selection.Field.Name;
        var fieldPath = context.Path.ToString();
        var services = context.Services;
        var ct = context.RequestAborted;

        var debugContext = services.GetService<IRequestDebugContext>();
        var beforeActions = services.GetServices<IBeforeAction>();
        var afterActions = services.GetServices<IAfterAction>();
        var transforms = services.GetServices<ITransformResult>();

        var interceptorCtx = new InterceptorContext
        {
            OperationName = operationName,
            FieldPath = fieldPath ?? operationName,
            Arguments = ExtractArguments(context),
            DebugContext = debugContext ?? new RequestDebugContext(Guid.NewGuid().ToString("N")),
            Services = services
        };

        // --- Before Actions ---
        var matchedBefore = beforeActions
            .Where(a => a.Operations.Count == 0 || a.Operations.Contains(operationName))
            .OrderBy(a => a.Order);

        foreach (var action in matchedBefore)
        {
            await action.ExecuteAsync(interceptorCtx, ct);
        }

        // --- Execute Resolver ---
        await _next(context);

        var result = context.Result;

        // --- Transform Results ---
        var matchedTransforms = transforms
            .Where(t => t.Operations.Count == 0 || t.Operations.Contains(operationName))
            .OrderBy(t => t.Order);

        foreach (var transform in matchedTransforms)
        {
            result = await transform.TransformAsync(interceptorCtx, result, ct);
        }

        if (!ReferenceEquals(result, context.Result))
            context.Result = result;

        // --- After Actions (fire-and-forget via BackgroundActionChannel) ---
        var matchedAfter = afterActions
            .Where(a => a.Operations.Count == 0 || a.Operations.Contains(operationName))
            .OrderBy(a => a.Order);

        var bgChannel = services.GetService<BackgroundActionChannel>();
        var logger = services.GetService<ILogger<InterceptorMiddleware>>();

        foreach (var action in matchedAfter)
        {
            if (bgChannel is not null)
            {
                // Enqueue for true fire-and-forget background processing
                var enqueued = bgChannel.TryEnqueue(new BackgroundActionItem
                {
                    Action = action,
                    Context = interceptorCtx,
                    Result = result
                });
                if (!enqueued)
                    logger?.LogWarning("BackgroundActionChannel full; dropping after-action {Action} for {Operation}",
                        action.GetType().Name, operationName);
            }
            else
            {
                // Fallback: execute inline if channel not registered
                try
                {
                    await action.ExecuteAsync(interceptorCtx, result, ct);
                }
                catch (Exception ex)
                {
                    logger?.LogWarning(ex, "After-action {Action} failed for {Operation}",
                        action.GetType().Name, operationName);
                }
            }
        }
    }

    private static IReadOnlyDictionary<string, object?> ExtractArguments(IMiddlewareContext context)
    {
        var dict = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var arg in context.Selection.Field.Arguments)
        {
            try
            {
                dict[arg.Name] = context.ArgumentValue<object?>(arg.Name);
            }
            catch
            {
                dict[arg.Name] = null;
            }
        }
        return dict;
    }
}
