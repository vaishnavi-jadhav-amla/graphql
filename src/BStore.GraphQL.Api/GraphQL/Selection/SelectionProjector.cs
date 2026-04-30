using HotChocolate.Resolvers;

namespace BStore.GraphQL.Api.GraphQL.Selection;

/// <summary>
/// ADR-012: helpers to inspect the selection set so resolvers project only what was requested.
/// EF queries already benefit from <c>[UseProjection]</c>; non-EF resolvers (HTTP/Azure Search/SQL stored procs)
/// should call <see cref="IsSelected"/> before fetching expensive sub-fields.
/// </summary>
public static class SelectionProjector
{
    /// <summary>True when the GraphQL request explicitly selected <paramref name="fieldName"/> on the current type.</summary>
    public static bool IsSelected(IResolverContext ctx, string fieldName)
    {
        if (ctx is null || string.IsNullOrEmpty(fieldName)) return false;
        foreach (var s in ctx.GetSelections(ctx.Selection.Type.NamedType<HotChocolate.Types.IObjectType>()))
        {
            if (string.Equals(s.Field.Name, fieldName, StringComparison.Ordinal))
                return true;
        }
        return false;
    }

    /// <summary>True when none of the listed sub-fields were requested (skip an expensive sub-query entirely).</summary>
    public static bool NoneSelected(IResolverContext ctx, params string[] fieldNames)
    {
        foreach (var f in fieldNames)
            if (IsSelected(ctx, f)) return false;
        return true;
    }
}
