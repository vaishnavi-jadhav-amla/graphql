using BStore.GraphQL.Api.Diagnostics;
using HotChocolate.Resolvers;

namespace BStore.GraphQL.Api.Auth.FieldPermissions;

/// <summary>
/// HotChocolate field middleware that enforces <see cref="RequirePermissionAttribute"/>
/// on individual GraphQL fields. Attached to fields by <see cref="FieldPermissionTypeInterceptor"/>.
///
/// <para><b>Behaviour:</b> If the user lacks the required role/permission, the field resolves
/// to <c>null</c> and a partial error is added to the response. The rest of the query
/// continues executing (no hard failure).</para>
/// </summary>
public sealed class FieldPermissionMiddleware(FieldDelegate next)
{
    /// <summary>
    /// Context data key used by <see cref="FieldPermissionTypeInterceptor"/> to attach
    /// the requirement metadata to each protected field.
    /// </summary>
    internal const string RequirementKey = "BStore.FieldPermission";

    public async Task InvokeAsync(IMiddlewareContext context)
    {
        // Retrieve the requirement that was attached by the type interceptor.
        if (!context.Selection.Field.ContextData.TryGetValue(RequirementKey, out var raw)
            || raw is not FieldPermissionRequirement requirement)
        {
            // No requirement metadata — pass through (should not happen).
            await next(context);
            return;
        }

        var services = context.Services;
        var evaluator = services.GetRequiredService<IFieldPermissionEvaluator>();
        var httpAccessor = services.GetService<IHttpContextAccessor>();
        var user = httpAccessor?.HttpContext?.User;
        var ct = context.RequestAborted;

        var authorized = await evaluator.IsAuthorizedAsync(user, requirement, ct);

        if (authorized)
        {
            await next(context);
            return;
        }

        // --- Access denied: null the field and report a partial error ---
        context.Result = null;

        var debug = services.GetService<IRequestDebugContext>();
        var correlationId = debug?.CorrelationId ?? "n/a";
        var fieldPath = context.Path.ToString();

        var message = requirement.DeniedMessage
            ?? $"You do not have permission to access '{requirement.FieldName}'.";

        context.ReportError(
            ErrorBuilder.New()
                .SetMessage(message)
                .SetCode("FIELD_ACCESS_DENIED")
                .SetPath(context.Path)
                .SetExtension("code", "FIELD_ACCESS_DENIED")
                .SetExtension("category", "Authorization")
                .SetExtension("correlationId", correlationId)
                .SetExtension("field", requirement.FieldName)
                .SetExtension("type", requirement.TypeName)
                .SetExtension("timestamp", DateTime.UtcNow.ToString("o"))
                .Build());
    }
}
