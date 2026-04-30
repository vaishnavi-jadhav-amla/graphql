using BStore.GraphQL.Api.Configuration;
using BStore.GraphQL.Api.Diagnostics;
using BStore.GraphQL.Api.GraphQL.Infrastructure;
using BStore.GraphQL.Api.Infrastructure;
using BStore.GraphQL.Api.Middleware;
using HotChocolate.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<ZnodeApiOptions>(
    builder.Configuration.GetSection(ZnodeApiOptions.Section));

builder.Services.Configure<GraphQLOptions>(
    builder.Configuration.GetSection(GraphQLOptions.Section));

builder.AddBStoreCachingAndMessaging();

var graphqlOpts = builder.Configuration
    .GetSection(GraphQLOptions.Section)
    .Get<GraphQLOptions>() ?? new GraphQLOptions();

builder.AddBStoreGraphQlStack();

var app = builder.Build();

if (app.Environment.IsDevelopment())
    app.UseDeveloperExceptionPage();

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseRouting();

if (app.Environment.IsDevelopment() && graphqlOpts.EnableDevCors)
    app.UseCors("BStoreGraphQLDev");

app.MapHealthChecks("/health").AllowAnonymous();

// ADR-025/026: provider health is admin-only — guarded by the same admin token used for Detailed debug.
app.MapGet("/health/providers", (IProviderHealthTracker tracker, IRequestDebugContext ctx) =>
    !ctx.IsAdmin
        ? Results.Forbid()
        : Results.Json(tracker.Snapshot()));

app.MapGet("/", () => Results.Redirect("/graphql")).AllowAnonymous();

var graphql = app.MapGraphQL("/graphql").WithOptions(new GraphQLServerOptions
{
    Tool =
    {
        Enable = app.Environment.IsDevelopment(),
        Title  = "BStore GraphQL (Znode-aligned)"
    }
});

if (app.Environment.IsDevelopment() && graphqlOpts.EnableDevCors)
    graphql.RequireCors("BStoreGraphQLDev");

app.Run();
