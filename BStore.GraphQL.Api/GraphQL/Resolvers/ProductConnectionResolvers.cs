using BStore.GraphQL.Api.Diagnostics;
using BStore.GraphQL.Api.GraphQL.Queries;
using BStore.GraphQL.Api.GraphQL.Types;
using HotChocolate;
using HotChocolate.Data;
using HotChocolate.Types;
using Microsoft.EntityFrameworkCore;
using Znode.Libraries.Data.ZnodeEntity;

namespace BStore.GraphQL.Api.GraphQL.Resolvers;

/// <summary>
/// ADR-010: cursor-based (Relay) pagination over published products.
/// Page-size enforcement (ADR-007) is centralised in <c>ModifyPagingOptions</c>; OFFSET-style "give me page 50"
/// requests are absent because this connection only exposes <c>first/after</c> and <c>last/before</c>.
/// ADR-012: <c>[UseProjection]</c> drops un-selected columns from the EF SQL automatically.
/// </summary>
[ExtendObjectType(typeof(BStoreQuery))]
public sealed class ProductConnectionResolvers
{
    [GraphQLName("productsConnection")]
    [GraphQLDescription("Cursor-based product connection (Relay). Use first/after, last/before. ADR-010.")]
    [UsePaging(IncludeTotalCount = true)]
    [UseProjection]
    [UseFiltering]
    [UseSorting]
    public IQueryable<ProductRow> ProductsConnection(
        [Service] Znode_Entities db,
        [Service] IRequestDebugContext debug)
    {
        debug.RecordDataSource(DataSource.ZnodeEntities);
        debug.Note("product.connection", "cursor-based productsConnection");

        return db.ZnodePublishProductDetails.AsNoTracking()
            .Where(d => d.PublishProductId != null)
            .Select(d => new ProductRow
            {
                Id          = d.PublishProductId!.Value,
                Title       = d.ProductName ?? "",
                Sku         = d.SKU,
                Description = "",
                Category    = "",
                Tags        = new List<string>(),
                Images      = new List<string>()
            });
    }
}
