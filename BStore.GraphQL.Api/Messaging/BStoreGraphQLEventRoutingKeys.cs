namespace BStore.GraphQL.Api.Messaging;

/// <summary>Topic routing keys bound under <c>bstore.graphql.#</c> in appsettings.</summary>
public static class BStoreGraphQLEventRoutingKeys
{
    public const string BStoreActivationChanged = "bstore.graphql.mutation.bstoreActivationChanged";
    public const string BStoreSettingsUpdated   = "bstore.graphql.mutation.bstoreSettingsUpdated";
    public const string BStoreThemeUpdated      = "bstore.graphql.mutation.bstoreThemeUpdated";
}
