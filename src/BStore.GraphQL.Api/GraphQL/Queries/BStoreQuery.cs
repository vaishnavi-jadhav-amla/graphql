namespace BStore.GraphQL.Api.GraphQL.Queries;

/// <summary>
/// GraphQL query root marker only. All query fields are composed via
/// <c>[ExtendObjectType(typeof(BStoreQuery))]</c> resolvers in <c>GraphQL/Resolvers/</c> — this type performs
/// no data access and does not reference application or HTTP clients.
/// </summary>
public sealed class BStoreQuery;
