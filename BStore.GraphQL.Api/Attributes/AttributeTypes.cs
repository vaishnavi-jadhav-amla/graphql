using HotChocolate;

namespace BStore.GraphQL.Api.Attributes;

/// <summary>
/// ADR-001: portal/product configuration is exposed via attribute groups, never as a flat hard-coded
/// type. Adding a new attribute in PIM appears here automatically with no schema change.
/// </summary>
public sealed class GlobalAttributeGroup
{
    public int GroupId { get; init; }
    public string Code { get; init; } = "";
    public string Name { get; init; } = "";
    public IReadOnlyList<GlobalAttribute> Attributes { get; init; } = [];
}

public sealed class GlobalAttribute
{
    public int AttributeId { get; init; }

    [GraphQLName("code")]
    public string Code { get; init; } = "";

    public string Name { get; init; } = "";
    public string? Value { get; init; }
    public string? DataType { get; init; }
    public string? Locale { get; init; }
}
