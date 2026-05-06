---
paths:
  - "**/Diagnostics/**/*.cs"
  - "**/Logging/**/*.cs"
---

# Diagnostics Layer Rules (ADR-018 through ADR-028)

## IRequestDebugContext — Per-Request Debug State

Every request gets a scoped `IRequestDebugContext`. Services, pipeline steps, and providers call into it. It is a no-op when debug mode is `off`.

```csharp
public interface IRequestDebugContext
{
    string CorrelationId { get; }           // req_{8-char-hex}
    DebugLevel Level { get; }               // off | basic | trace | diagnose
    void RecordSource(string path, string source, string? cache, long latencyMs);
    void RecordTiming(string stage, long durationMs);
    void RecordStep(string name, string status, long durationMs, string? reason = null, Exception? ex = null);
    IReadOnlyList<DataSourceRecord> DataSources { get; }
    IReadOnlyList<TimingRecord> Timings { get; }
}
```

**File location:** `Diagnostics/IRequestDebugContext.cs` + `Diagnostics/RequestDebugContext.cs`

## CorrelationIdMiddleware

- Runs before all other middleware.
- Reads `X-Correlation-Id` header — if present, use it. If absent, generate `req_{Guid[..8]}`.
- Sets `IRequestDebugContext.CorrelationId`.
- Appends `X-Correlation-Id` to the response.
- **File:** `Diagnostics/CorrelationIdMiddleware.cs`

## DebugResponseMiddleware

- Reads `X-Debug-Level` header — valid values: `basic`, `trace`, `diagnose`.
- **Only honors the header if JWT has `role=Admin` OR a valid debug-unlock token is present.** Customer JWTs always get `off`.
- Writes `extensions.correlationId`, `extensions.timings`, `extensions.dataSources`, `extensions.pipeline` to the response based on the level.
- **File:** `Diagnostics/DebugResponseMiddleware.cs`

## IEmptyResultDiagnoser — Required for Every List Operation

When a list query returns empty and debug level ≥ `diagnose`, the diagnoser runs and populates `extensions.diagnosis`.

```csharp
public interface IEmptyResultDiagnoser
{
    string Operation { get; }   // matches the GraphQL operation name

    Task<DiagnosisResult> DiagnoseAsync(
        IReadOnlyDictionary<string, object?> args,
        CancellationToken ct);
}

public class DiagnosisResult
{
    public string Summary { get; set; } = string.Empty;
    public List<DiagnosticCheck> Checks { get; set; } = new();
    public List<string> Suggestions { get; set; } = new();
}

public class DiagnosticCheck
{
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;  // pass | fail | warn | info
    public string Message { get; set; } = string.Empty;
    public string? Hint { get; set; }
    public string? VerifyQuery { get; set; }   // optional SQL to verify manually
}
```

### Diagnoser Check Order (cheapest → most expensive, most likely → least likely)

1. Root entity exists (portal, account, cart) — direct indexed lookup
2. Authorization scope — is this user allowed to see this data?
3. Multi-tenant mapping — portal → catalog → category → product chain
4. Publish state — is the data published and not expired?
5. Provider health — if data comes from a provider, is it up?

### Example Diagnoser Skeleton

```csharp
// Diagnostics/Diagnoses/ProductListDiagnoser.cs
public class ProductListDiagnoser : IEmptyResultDiagnoser
{
    public string Operation => "getProductsBySeoUrl";

    public async Task<DiagnosisResult> DiagnoseAsync(
        IReadOnlyDictionary<string, object?> args, CancellationToken ct)
    {
        var result = new DiagnosisResult();
        var portalId = args.GetInt("portalId");
        var seoUrl = args.GetString("seoUrl");

        // Check 1: Portal exists
        var portal = await _db.ZnodePortals.FirstOrDefaultAsync(p => p.PortalId == portalId, ct);
        result.Checks.Add(new DiagnosticCheck
        {
            Name = "PortalExists",
            Status = portal is null ? "fail" : "pass",
            Message = portal is null ? $"Portal {portalId} not found" : "Portal found",
            Hint = portal is null ? "Verify portalId is correct in the request" : null
        });

        // ... more checks ...
        result.Summary = result.Checks.Any(c => c.Status == "fail")
            ? $"{result.Checks.Count(c => c.Status == "fail")} check(s) failed"
            : "All checks passed";

        return result;
    }
}
```

### Registration

```csharp
// In GraphQLServiceRegistration.RegisterServices():
services.AddScoped<IEmptyResultDiagnoser, ProductListDiagnoser>();
services.AddScoped<IEmptyResultDiagnoser, CategoryDiagnoser>();
services.AddScoped<IEmptyResultDiagnoser, CartDiagnoser>();
// ...
```

## ProviderHealthTracker

Records call metrics for every external provider call. Exposed via admin `providers` query.

```csharp
public interface IProviderHealthTracker
{
    void RecordSuccess(string providerName, long latencyMs, bool fromCache);
    void RecordFailure(string providerName, string errorMessage, long latencyMs);
    ProviderHealthSnapshot GetSnapshot(string providerName);
}
```

**File:** `Diagnostics/ProviderHealthTracker.cs` — registered as singleton (accumulates across requests).

## IPipelineStepTracer

Each pipeline step injects and calls this tracer. `PipelineExecutor` creates one per operation and exposes results in `extensions.pipeline.steps`.

```csharp
public interface IPipelineStepTracer
{
    void RecordStart(string stepName);
    void RecordComplete(string stepName, long durationMs);
    void RecordSkipped(string stepName, string reason);
    void RecordFailed(string stepName, Exception ex, long durationMs);
}
```

**File:** `Diagnostics/PipelineTracer.cs`

## Files Required for This Layer

| File | Purpose |
|---|---|
| `Diagnostics/IRequestDebugContext.cs` | Per-request debug context interface |
| `Diagnostics/RequestDebugContext.cs` | Scoped implementation |
| `Diagnostics/NullRequestDebugContext.cs` | No-op for when debug is off |
| `Diagnostics/CorrelationIdMiddleware.cs` | Assigns correlationId to every request |
| `Diagnostics/DebugResponseMiddleware.cs` | Writes extensions to GraphQL response |
| `Diagnostics/ProviderHealthTracker.cs` | Singleton — accumulates provider metrics |
| `Diagnostics/PipelineTracer.cs` | Per-request pipeline step recording |
| `Diagnostics/Exceptions/` | Custom exception types (one file per type) |
| `Diagnostics/Diagnoses/*.cs` | One diagnoser per list operation |

## Forbidden Patterns

- Do not call `Console.WriteLine` for debug output — use `IRequestDebugContext`.
- Do not add `try/catch/swallow` to hide errors from the diagnostic system.
- Do not skip `RecordSource()` after a DB call — this is how `extensions.dataSources` is populated.
- Do not write a new list operation without a matching `IEmptyResultDiagnoser`.
