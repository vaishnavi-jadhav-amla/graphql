---
paths:
  - "**/Pipeline/**/*.cs"
  - "**/Interceptors/**/*.cs"
---

# Pipeline & Interceptor Rules

## Pipeline Steps
- Implement `IPipelineStep<TContext>` with a unique `StepName` and `Order` (multiples of 100)
- Steps execute sequentially by `Order` — lower numbers run first
- Steps can be disabled via `appsettings.json` → `GraphQL:Pipeline:DisabledSteps`
- Keep steps focused on a single responsibility (validate, calculate, persist, notify)
- Pipeline context is mutable — each step reads/writes shared state

## Interceptors
- `IBeforeAction` runs BEFORE the resolver — use for validation, logging, rate limiting
- `IAfterAction` runs AFTER the resolver — use for side effects (ERP sync, webhooks). Default `FireAndForget = true`
- `ITransformResult` modifies the resolver result — use for enrichment (external pricing, inventory)
- `Operations` list supports wildcards: `"*"` = all, `"product*"` = prefix match
- Interceptors are auto-discovered from DI — register in `GraphQLServiceRegistration.cs`
- NEVER throw exceptions from `IAfterAction` with `FireAndForget = true` — failures are logged, not propagated
