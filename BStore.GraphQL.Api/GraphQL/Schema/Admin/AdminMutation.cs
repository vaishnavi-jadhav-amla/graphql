namespace BStore.GraphQL.Api.GraphQL.Schema.Admin;

/// <summary>
/// Root mutation type for the admin schema (<c>/graphql/admin</c>).
/// Admin-only writes: B-store CRUD, user management, activation, theme.
/// All fields are composed via <c>[ExtendObjectType(typeof(AdminMutation))]</c>.
/// </summary>
public sealed class AdminMutation;
