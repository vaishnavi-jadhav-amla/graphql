namespace BStore.GraphQL.Api.GraphQL.Schema.Admin;

/// <summary>
/// Root query type for the admin schema (<c>/graphql/admin</c>).
/// Admin-only operations: B-store management, user management, diagnostics, all orders, all accounts.
/// All fields are composed via <c>[ExtendObjectType(typeof(AdminQuery))]</c>.
/// </summary>
public sealed class AdminQuery;
