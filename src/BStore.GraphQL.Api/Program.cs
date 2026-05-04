using BStore.GraphQL.Api.Auth;
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

// 1. Authentication & Authorization (JWT + API Key + policies + IDOR guards)
builder.AddBStoreAuth();

// 2. Caching & Messaging (L1/L2 cache + RabbitMQ)
builder.AddBStoreCachingAndMessaging();

// 3. Rate Limiting (HTTP-level: global, mutations, auth, admin)
builder.AddBStoreRateLimiting();

var graphqlOpts = builder.Configuration
    .GetSection(GraphQLOptions.Section)
    .Get<GraphQLOptions>() ?? new GraphQLOptions();

// 4. GraphQL stack (EF, resolvers, interceptors, pipelines, providers, DataLoaders, HC)
builder.AddBStoreGraphQlStack();

var app = builder.Build();

if (app.Environment.IsDevelopment())
    app.UseDeveloperExceptionPage();

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseRouting();

// Rate limiting middleware (must be after routing, before auth)
var rateLimitSettings = builder.Configuration
    .GetSection(RateLimitSettings.Section)
    .Get<RateLimitSettings>() ?? new RateLimitSettings();
if (rateLimitSettings.Enabled)
    app.UseRateLimiter();

if (app.Environment.IsDevelopment() && graphqlOpts.EnableDevCors)
    app.UseCors("BStoreGraphQLDev");

// Authentication & Authorization middleware
app.UseAuthentication();
app.UseAuthorization();

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

if (rateLimitSettings.Enabled)
    graphql.RequireRateLimiting(RateLimitingRegistration.PolicyGlobal);

if (app.Environment.IsDevelopment() && graphqlOpts.EnableDevCors)
    graphql.RequireCors("BStoreGraphQLDev");

app.Run();
