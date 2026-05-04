namespace BStore.GraphQL.Api.GraphQL.Schema.Storefront;

/// <summary>
/// Root query type for the storefront schema (<c>/graphql/storefront</c>).
/// Customer-facing operations: products, categories, cart, account (own), orders (own).
/// All fields are composed via <c>[ExtendObjectType(typeof(StorefrontQuery))]</c>.
/// </summary>
public sealed class StorefrontQuery;
