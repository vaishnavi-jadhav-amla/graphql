namespace BStore.GraphQL.Api.GraphQL.Schema.Storefront;

/// <summary>
/// Root mutation type for the storefront schema (<c>/graphql/storefront</c>).
/// Customer-facing writes: cart, checkout, wishlist, address, auth.
/// All fields are composed via <c>[ExtendObjectType(typeof(StorefrontMutation))]</c>.
/// </summary>
public sealed class StorefrontMutation;
