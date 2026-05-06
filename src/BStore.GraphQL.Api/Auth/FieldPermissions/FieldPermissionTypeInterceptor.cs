using System.Reflection;
using HotChocolate.Configuration;
using HotChocolate.Types;
using HotChocolate.Types.Descriptors;
using HotChocolate.Types.Descriptors.Definitions;

namespace BStore.GraphQL.Api.Auth.FieldPermissions;

/// <summary>
/// HotChocolate type interceptor that scans all object types for properties annotated
/// with <see cref="RequirePermissionAttribute"/> and automatically wires
/// <see cref="FieldPermissionMiddleware"/> onto those fields.
///
/// <para>This is the glue between the declarative attribute and the runtime middleware.
/// Registered once on the HotChocolate builder — no per-type configuration needed.</para>
/// </summary>
public sealed class FieldPermissionTypeInterceptor : TypeInterceptor
{
    public override void OnBeforeCompleteType(
        ITypeCompletionContext completionContext,
        DefinitionBase definition)
    {
        if (definition is not ObjectTypeDefinition objectTypeDef)
            return;

        var runtimeType = objectTypeDef.RuntimeType;
        if (runtimeType == typeof(object) || runtimeType.Namespace?.StartsWith("HotChocolate") == true)
            return;

        foreach (var fieldDef in objectTypeDef.Fields)
        {
            var member = fieldDef.Member ?? fieldDef.ResolverMember;
            if (member is null)
                continue;

            var attr = member.GetCustomAttribute<RequirePermissionAttribute>();
            if (attr is null && member is MethodInfo)
            {
                // For code-first properties exposed via getter methods, check the property too.
                var prop = runtimeType.GetProperty(fieldDef.Name,
                    BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                attr = prop?.GetCustomAttribute<RequirePermissionAttribute>();
            }

            if (attr is null)
                continue;

            var requirement = new FieldPermissionRequirement
            {
                Roles = attr.Roles,
                Permissions = attr.Permissions,
                FieldName = fieldDef.Name,
                TypeName = objectTypeDef.Name,
                DeniedMessage = attr.DeniedMessage
            };

            // Attach the requirement to the field's context data so the middleware can read it.
            fieldDef.ContextData[FieldPermissionMiddleware.RequirementKey] = requirement;

            // Prepend the permission middleware so it runs before the resolver.
            fieldDef.MiddlewareDefinitions.Insert(0,
                new FieldMiddlewareDefinition(
                    next => async context => await new FieldPermissionMiddleware(next).InvokeAsync(context)));
        }
    }
}
