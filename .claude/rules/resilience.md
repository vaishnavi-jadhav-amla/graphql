---
paths:
  - "**/Services/**/*.cs"
  - "**/Pipeline/**/*.cs"
  - "**/Providers/**/*.cs"
  - "**/Interceptors/**/*.cs"
---

# Resilience & Fault Tolerance Rules

## External Provider Calls

- **Never let a provider failure crash a storefront page.** Use `FallbackToZnode: true` in provider config + catch `ProviderTimeoutException`, `ProviderEmptyException`, `ProviderHttpException` at the enrichment site.
- **Always respect `TimeoutMs` from provider config.** Wrap every HTTP call with `CancellationToken` derived from `TimeoutMs`:
  ```csharp
  using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
  cts.CancelAfter(TimeSpan.FromMilliseconds(config.TimeoutMs));
  ```
- **Throw the correct typed exception on provider failure** — never return empty silently:
  - Timeout → `ProviderTimeoutException`
  - Empty/unexpected response → `ProviderEmptyException`
  - HTTP 4xx/5xx → `ProviderHttpException`
- **Log provider failure at `Warning` level** (recoverable, fallback applied) — never swallow silently.
- **Record provider health metrics via `ProviderHealthTracker`** after every call — success or failure.

## Pipeline Steps

- **Each step is responsible for its own exception handling.** If step N throws and does not handle it, `PipelineExecutor` marks it `failed` and marks all subsequent steps `not-run`.
- **Critical vs non-critical steps:**
  - Steps 100 (ValidateCart), 500 (CreateOrderRecord), 600 (ProcessPayment) are **critical** — failure halts the pipeline.
  - Steps 300 (Discounts), 700 (Confirmation) are **non-critical** — failure logs at `Warning` and the step is marked `skipped`, pipeline continues.
  - Mark a step as non-critical by setting `IsCritical = false` on the `IPipelineStep` implementation.
- **Each step must call `tracer.RecordStart()` at the top and `tracer.RecordComplete()` / `tracer.RecordFailed(ex)` at the bottom.** Pipeline tracing requires this — see ADR-024.
- **Steps must be idempotent where possible.** If an order record was created in step 500 before payment failed in step 600, re-running must not create duplicate records.
- **Never skip the payment step programmatically from within another step.** Disable via config only (`DisabledSteps`).

## Services

- **Graceful degradation over hard failure.** When a non-essential sub-operation fails (e.g., loading extended attributes), log and continue with partial data — do not fail the entire parent query.
- **Do not catch and swallow `OperationCanceledException` / `TaskCanceledException`** — these propagate intentional cancellation. Let them bubble.
- **Use `ConfigureAwait(false)` on all non-UI awaits** to avoid deadlocks in synchronization contexts.
- **Retry policy for transient SQL errors:**
  - Max retries: 3, back-off: 100ms/200ms/400ms
  - Retry only on `SqlException` with transient error codes (1205 deadlock, 40613 DB unavailable)
  - Never retry: constraint violations, auth failures, business logic exceptions
- **Circuit breaker for external providers (future):** If a provider errors more than 5x in 60s, open the circuit and return fallback immediately without attempting the call.

## Cache Failures

- **L2 (Redis) failure must not take down the API.** Wrap all `IL2Cache` calls in `try/catch` — fall through to DB on failure:
  ```csharp
  try { cached = await _l2.GetAsync<T>(key, ct); }
  catch (Exception ex) { _logger.LogWarning(ex, "L2 cache unavailable | ..."); }
  if (cached is null) { /* load from DB */ }
  ```
- **L1 (memory) failure is unexpected.** If it throws, let it propagate — it is a critical infrastructure failure.
- **SemaphoreSlim stampede protection must be used on all L2 cache populations** for hot keys (category tree, portal identity). See `project_redis_optimization.md`.

## Partial Results

- **A single DataLoader failure in a list should not null out the entire list.** `BatchDataLoader` implementations should catch per-key exceptions and return `null` for failed keys, logging each.
- **An empty list is always valid.** Never throw when a list query returns zero rows — let `IEmptyResultDiagnoser` explain it.
- **Provider-enriched fields (price, inventory) can be omitted from the response** if the provider is down, as long as the base product data is returned. The field is `null`, not an error.

## No Silent Failures

- **`try { ... } catch { }` without re-throw or log is forbidden.** If intentionally swallowed, add `// INTENTIONAL-SWALLOW: <reason>` comment.
- **Background fire-and-forget (`IAfterAction.FireAndForget = true`) must still catch and log exceptions.** Unobserved task exceptions are not acceptable.
