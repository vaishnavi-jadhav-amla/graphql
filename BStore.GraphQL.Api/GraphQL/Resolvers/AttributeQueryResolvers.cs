using BStore.GraphQL.Api.Attributes;
using BStore.GraphQL.Api.Diagnostics;
using BStore.GraphQL.Api.GraphQL.Queries;
using HotChocolate;
using HotChocolate.Types;

namespace BStore.GraphQL.Api.GraphQL.Resolvers;

/// <summary>ADR-001: <c>globalAttributeGroups</c> exposes attribute-driven configuration.</summary>
[ExtendObjectType(typeof(BStoreQuery))]
public sealed class AttributeQueryResolvers
{
    [GraphQLDescription("Attribute-driven configuration groups for a portal (ADR-001).")]
    public Task<IReadOnlyList<GlobalAttributeGroup>> GlobalAttributeGroups(
        int portalId,
        string? locale,
        [Service] IAttributeGroupReadService reader,
        [Service] IRequestDebugContext debug,
        CancellationToken ct)
    {
        debug.Note("attributes.globalGroups", $"portalId={portalId} locale={locale}");
        return reader.GetGroupsAsync(portalId, locale ?? "en-US", ct);
    }
}
