namespace BStore.GraphQL.Api.Attributes;

/// <summary>ADR-001: read attribute groups from <c>ZnodePublish_Entities</c> only (ADR-009).</summary>
public interface IAttributeGroupReadService
{
    Task<IReadOnlyList<GlobalAttributeGroup>> GetGroupsAsync(int portalId, string locale, CancellationToken ct);
}
