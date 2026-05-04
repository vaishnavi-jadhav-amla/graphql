---
paths:
  - "**/Queries/**/*.cs"
  - "**/Mutations/**/*.cs"
  - "**/Schema/**/*.cs"
---

# GraphQL Resolver Rules

- Every query/mutation class MUST use `[ExtendObjectType]` targeting the correct root type:
  - Storefront queries → `[ExtendObjectType(typeof(StorefrontQuery))]`
  - Storefront mutations → `[ExtendObjectType(typeof(StorefrontMutation))]`
  - Admin queries → `[ExtendObjectType(typeof(AdminQuery))]`
  - Admin mutations → `[ExtendObjectType(typeof(AdminMutation))]`
- NEVER share a resolver class between Storefront and Admin schemas
- All resolver methods MUST be `async Task<T>` — no synchronous DB access
- Inject services via method-level `[Service]` attribute, not constructor injection
- Include `[Authorize(Policy = "...")]` on every resolver method — no unauthenticated resolvers
- Use `CancellationToken` parameter in all resolver methods
- Return domain types from `Types/` folder, never EF Core entities directly
- Register every new query/mutation class in `GraphQLServiceRegistration.cs` under the correct schema
