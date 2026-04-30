using BStore.GraphQL.Api.Application;
using BStore.GraphQL.Api.Attributes;
using BStore.GraphQL.Api.Bulk;
using BStore.GraphQL.Api.Caching;
using BStore.GraphQL.Api.Catalog;
using BStore.GraphQL.Api.Configuration;
using BStore.GraphQL.Api.Data;
using BStore.GraphQL.Api.DataLoaders;
using BStore.GraphQL.Api.Diagnostics;
using BStore.GraphQL.Api.GraphQL.Mutations;
using BStore.GraphQL.Api.GraphQL.Queries;
using BStore.GraphQL.Api.GraphQL.Resolvers;
using BStore.GraphQL.Api.Media;
using BStore.GraphQL.Api.Search;
using BStore.GraphQL.Api.Services;
using BStore.GraphQL.Api.Storefront;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Znode.Libraries.Data.ZnodeEntity;

namespace BStore.GraphQL.Api.GraphQL.Infrastructure;

/// <summary>
/// Central GraphQL + Znode EF registration (mirrors <c>Znode.Engine.GraphQL.GraphQLServiceRegistration</c> layout:
/// retrying SQL, DbContext factory, HttpContextAccessor, HC error filter, diagnostics, depth and paging limits).
/// </summary>
public static class BStoreGraphQLServiceRegistration
{
    /// <summary>
    /// Registers Znode <see cref="Znode_Entities"/>, data/application services, auth HttpClient, resolver types, and Hot Chocolate.
    /// </summary>
    public static WebApplicationBuilder AddBStoreGraphQlStack(this WebApplicationBuilder builder)
    {
        var gql = builder.Configuration.GetSection(GraphQLOptions.Section).Get<GraphQLOptions>() ?? new GraphQLOptions();

        if (builder.Environment.IsDevelopment() && gql.EnableDevCors)
        {
            builder.Services.AddCors(o =>
                o.AddPolicy("BStoreGraphQLDev", p =>
                    p.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));
        }

        var rawZnodeConn = builder.Configuration.GetConnectionString("Znode_Entities");
        if (string.IsNullOrWhiteSpace(rawZnodeConn))
            throw new InvalidOperationException("ConnectionStrings:Znode_Entities is required for B-store database access.");

        var rawPublishConn = builder.Configuration.GetConnectionString("ZnodePublish_Entities") ?? rawZnodeConn;

        // ADR-005: ensure Max Pool Size = SqlMaxPoolSize on every SQL connection.
        var znodeEntitiesConn   = ApplyPoolSize(rawZnodeConn,   gql.SqlMaxPoolSize);
        var publishEntitiesConn = ApplyPoolSize(rawPublishConn, gql.SqlMaxPoolSize);

        builder.Services.AddHttpContextAccessor();

        void ConfigureDb(DbContextOptionsBuilder options) =>
            options.UseSqlServer(znodeEntitiesConn, sql =>
                sql.EnableRetryOnFailure(
                    maxRetryCount: 3,
                    maxRetryDelay: TimeSpan.FromMilliseconds(400),
                    errorNumbersToAdd: new[] { 1205 }));

        void ConfigurePublishDb(DbContextOptionsBuilder options) =>
            options.UseSqlServer(publishEntitiesConn, sql =>
                sql.EnableRetryOnFailure(
                    maxRetryCount: 3,
                    maxRetryDelay: TimeSpan.FromMilliseconds(400),
                    errorNumbersToAdd: new[] { 1205 }));

        // Order matters: register the singleton factory first, then add the scoped DbContext using
        // the same options registered as Singleton (otherwise scoped DbContextOptions<T> conflicts
        // with the singleton factory). Equivalent to passing optionsLifetime: Singleton.
        builder.Services.AddDbContextFactory<Znode_Entities>(ConfigureDb);
        builder.Services.AddDbContext<Znode_Entities>(ConfigureDb, optionsLifetime: ServiceLifetime.Singleton);

        // ADR-009: storefront reads come exclusively from ZnodePublish_Entities (no EAV joins).
        builder.Services.AddDbContextFactory<ZnodePublish_Entities>(ConfigurePublishDb);

        // ADR-018/022/023/024/025/027: per-request debug context + provider health tracking.
        builder.Services.AddSingleton<IProviderHealthTracker, ProviderHealthTracker>();
        builder.Services.AddScoped<IRequestDebugContext>(sp =>
        {
            var http = sp.GetService<IHttpContextAccessor>()?.HttpContext;
            var corr = http?.Request.Headers[Middleware.CorrelationIdMiddleware.HeaderName].ToString();
            if (string.IsNullOrWhiteSpace(corr)) corr = Guid.NewGuid().ToString("N");
            return new RequestDebugContext(corr);
        });

        // ADR-020: empty-result diagnosers (per operation, resolved by Operation key).
        builder.Services.AddScoped<IEmptyResultDiagnoser, ProductListEmptyResultDiagnoser>();
        builder.Services.AddScoped<IEmptyResultDiagnoser, BStoreListEmptyResultDiagnoser>();

        // ADR-014: CDN media URL rewrite.
        builder.Services.Configure<MediaOptions>(builder.Configuration.GetSection(MediaOptions.Section));
        builder.Services.AddSingleton<MediaUrlBuilder>();

        // ADR-016: centralised TTL profile (inventory/pricing ≤ 30s enforced here).
        builder.Services.AddSingleton<CacheTtlProfile>();

        // ADR-001 + ADR-009: attribute groups read from ZnodePublish_Entities.
        builder.Services.AddScoped<IAttributeGroupReadService, AttributeGroupReadService>();

        // ADR-015: materialised path category tree.
        builder.Services.AddScoped<ICategoryTreeService, MaterializedPathCategoryTreeService>();

        // ADR-011: search abstraction (SQL fallback by default; swap to Azure for production).
        builder.Services.Configure<SearchOptions>(builder.Configuration.GetSection(SearchOptions.Section));
        var searchProvider = builder.Configuration[$"{SearchOptions.Section}:Provider"] ?? "Sql";
        if (string.Equals(searchProvider, "Azure", StringComparison.OrdinalIgnoreCase))
            builder.Services.AddScoped<ISearchProvider, AzureCognitiveSearchProvider>();
        else
            builder.Services.AddScoped<ISearchProvider, SqlLikeSearchProvider>();

        // ADR-017: bulk write helper.
        builder.Services.AddScoped<IBulkWriter, SqlBulkCopyWriter>();

        builder.Services.AddScoped<IBStoreEfReadService, BStoreEfReadService>();
        builder.Services.AddScoped<IBStoreEfWriteService, BStoreEfWriteService>();
        builder.Services.AddScoped<IBStoreUserDataService, BStoreUserEfService>();
        builder.Services.AddScoped<IUserDataService, UserEfService>();
        builder.Services.AddScoped<IProductCatalogReadService, ProductEfReadService>();
        builder.Services.AddScoped<IBStoreApplicationService, BStoreApplicationService>();

        var znodeBase = builder.Configuration[$"{ZnodeApiOptions.Section}:BaseUrl"] ?? "https://localhost:54546";
        var timeoutSec = int.TryParse(builder.Configuration[$"{ZnodeApiOptions.Section}:TimeoutSeconds"], out var t)
            ? t
            : 30;

        builder.Services.AddHttpClient<IAuthApiClient, AuthApiClient>(c =>
        {
            c.BaseAddress = new Uri(znodeBase.TrimEnd('/') + "/");
            c.Timeout     = TimeSpan.FromSeconds(timeoutSec);
        });

        builder.Services.AddHealthChecks();

        builder.Services.AddScoped<BStoreQueryResolvers>();
        builder.Services.AddScoped<BStoreQueryableResolvers>();
        builder.Services.AddScoped<BStoreUserQueryResolvers>();
        builder.Services.AddScoped<UserQueryResolvers>();
        builder.Services.AddScoped<ProductQueryResolvers>();
        builder.Services.AddScoped<DiagnoseQueryResolvers>();
        builder.Services.AddScoped<WebsiteEntryResolvers>();
        builder.Services.AddScoped<ProductConnectionResolvers>();
        builder.Services.AddScoped<AttributeQueryResolvers>();
        builder.Services.AddScoped<BStoreMutation>();
        builder.Services.AddScoped<BStoreUserMutation>();
        builder.Services.AddScoped<UserMutation>();
        builder.Services.AddScoped<AuthMutation>();

        var hcBuilder = builder.Services
            .AddGraphQLServer()
            .AddQueryType<BStoreQuery>()
            .AddProjections()
            .AddFiltering()
            .AddSorting()
            .AddTypeExtension<BStoreQueryResolvers>()
            .AddTypeExtension<BStoreQueryableResolvers>()
            .AddTypeExtension<BStoreUserQueryResolvers>()
            .AddTypeExtension<UserQueryResolvers>()
            .AddTypeExtension<ProductQueryResolvers>()
            .AddTypeExtension<DiagnoseQueryResolvers>()
            .AddTypeExtension<WebsiteEntryResolvers>()
            .AddTypeExtension<ProductConnectionResolvers>()
            .AddTypeExtension<AttributeQueryResolvers>()
            .AddDataLoader<ProductByIdDataLoader>()
            .AddMutationType<BStoreMutation>()
            .AddTypeExtension<BStoreUserMutation>()
            .AddTypeExtension<UserMutation>()
            .AddTypeExtension<AuthMutation>()
            .AddUploadType()
            .AddErrorFilter<BStoreGraphQLErrorFilter>()
            .AddDiagnosticEventListener<BStoreGraphQLDiagnosticListener>()
            .AddMaxExecutionDepthRule(gql.MaxQueryDepth)
            .ModifyPagingOptions(o =>
            {
                o.MaxPageSize         = gql.MaxPageSize;     // ADR-007: cap connections at 100
                o.DefaultPageSize     = gql.DefaultRelayPageSize;
                o.IncludeTotalCount   = true;
                o.RequirePagingBoundaries = false;
            })
            .ModifyRequestOptions(o =>
                o.IncludeExceptionDetails = builder.Environment.IsDevelopment());

        // ADR-007: introspection off in production unless explicitly opted in.
        if (!builder.Environment.IsDevelopment() && !gql.EnableIntrospectionInProd)
        {
            hcBuilder.DisableIntrospection();
        }

        return builder;
    }

    private static string ApplyPoolSize(string connectionString, int max)
    {
        if (connectionString.IndexOf("Max Pool Size", StringComparison.OrdinalIgnoreCase) >= 0)
            return connectionString;
        var trimmed = connectionString.TrimEnd(' ', ';');
        return $"{trimmed};Max Pool Size={max};";
    }
}
