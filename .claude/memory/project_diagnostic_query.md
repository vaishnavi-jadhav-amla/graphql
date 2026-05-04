---
name: Diagnostic Query — diagnose(operation, args)
description: Admin-only GraphQL query that executes any operation with full tracing and returns a structured report of every stage, data source, cache hit, provider call, and empty-result cause.
type: project
---

## Purpose

Instead of digging through logs for 2-3 days to find why `getProducts` returned empty for portal 7, run one GraphQL query:

```graphql
query {
  diagnose(
    operation: "getProducts"
    args: { portalId: 7, categoryId: 180 }
  ) {
    result           # what the operation returned
    durationMs
    stages {
      name
      status         # ok | warn | error | skipped
      durationMs
      details
    }
    dataSources {
      path
      source         # sql / cache:l1 / cache:l2 / provider:X / default
      cache
      latencyMs
    }
    providers {
      name
      calls
      errors
      p95LatencyMs
      status
    }
    diagnosis {      # only populated if result was empty/null/error
      summary
      checks {
        name
        status       # pass | fail | info
        message
        hint
        query        # optional SQL to verify
      }
      suggestions
    }
    errors {
      code
      message
      stage
      context
    }
  }
}
```

## Example Investigation

Bug report: "Category page for Plumbing is empty on Maxwell's Hardware store"

Run:
```graphql
query {
  diagnose(operation: "getProductsBySeoUrl", args: {
    seoUrl: "Plumbing",
    portalId: 7
  }) {
    result
    durationMs
    dataSources { path source cache latencyMs }
    diagnosis {
      summary
      checks { name status message hint query }
      suggestions
    }
  }
}
```

Response:
```json
{
  "data": {
    "diagnose": {
      "result": { "items": [], "totalCount": 0 },
      "durationMs": 58,
      "dataSources": [
        { "path": "seoResolve", "source": "sql", "cache": "l2-hit", "latencyMs": 1 },
        { "path": "categoryLookup", "source": "sql", "cache": "miss", "latencyMs": 12 },
        { "path": "productList", "source": "sql", "cache": "miss", "latencyMs": 41 }
      ],
      "diagnosis": {
        "summary": "Product list is empty for category 'Plumbing' in portal 7",
        "checks": [
          { "name": "PortalExists",       "status": "pass", "message": "Portal 7 (Maxwell's Hardware) is active" },
          { "name": "SEOUrlResolves",     "status": "pass", "message": "SEO URL 'Plumbing' → categoryId 1335" },
          { "name": "CategoryExists",     "status": "pass", "message": "Category 1335 (Plumbing) exists" },
          { "name": "CategoryInCatalog",  "status": "fail",
            "message": "Category 1335 is NOT in catalog 14 (assigned to portal 7)",
            "hint": "Category exists in catalog 22, but portal 7 uses catalog 14",
            "query": "SELECT PimCatalogId FROM ZnodePimCategoryHierarchy WHERE CategoryId = 1335" },
          { "name": "ProductsInCategory", "status": "info",
            "message": "Category 1335 has 23 products — but none in portal 7's catalog (14)" }
        ],
        "suggestions": [
          "Map category 1335 to catalog 14 in admin, or",
          "Assign catalog 22 to portal 7 in admin, then republish"
        ]
      }
    }
  }
}
```

**Time to diagnose: 3 seconds. Time without this tool: 2-3 days.**

## Implementation

```csharp
// Queries/Admin/DiagnosticQueries.cs
[ExtendObjectType(typeof(AdminQuery))]
public class DiagnosticQueries
{
    [Authorize(Policy = AuthConstants.PolicyAdminOnly)]
    public async Task<DiagnosticResult> Diagnose(
        string operation,
        JsonElement args,
        [Service] IDiagnosticRunner runner,
        CancellationToken ct)
        => await runner.RunAsync(operation, args, ct);
}
```

`IDiagnosticRunner` implementation:
1. Create a per-call `RequestDebugContext` with `Level=diagnose`
2. Invoke the target operation via reflection/dispatcher
3. Capture all recorded stages, sources, provider calls
4. If result is empty/null/error, run the registered `IEmptyResultDiagnoser` for that operation
5. Return `DiagnosticResult` payload

## Supported Operations for Diagnosis

Start with the highest-pain operations:

| Operation | Empty-result diagnoser |
|---|---|
| `getProducts` / `getProductsBySeoUrl` | `ProductListDiagnoser` |
| `getCategory` / `getCategories` | `CategoryDiagnoser` |
| `getCart` | `CartDiagnoser` |
| `getOrder` / `getOrderByNumber` | `OrderDiagnoser` |
| `getPageBuilderPageBySlug` | `PageBuilderDiagnoser` |
| `websiteEntry` | `WebsiteEntryDiagnoser` |
| `searchProducts` | `SearchDiagnoser` |

Adding a new operation diagnoser = one class + one DI registration.

## Diagnoser Interface

```csharp
public interface IEmptyResultDiagnoser
{
    string OperationName { get; }

    Task<DiagnosisReport> DiagnoseAsync(
        JsonElement args,
        object? result,
        CancellationToken ct);
}

public class DiagnosisReport
{
    public string Summary { get; set; }
    public List<DiagnosisCheck> Checks { get; set; } = new();
    public List<string> Suggestions { get; set; } = new();
}

public class DiagnosisCheck
{
    public string Name { get; set; }
    public DiagnosisStatus Status { get; set; }   // Pass / Fail / Info / Warn
    public string Message { get; set; }
    public string? Hint { get; set; }
    public string? Query { get; set; }            // SQL or shell command to verify manually
}
```

## ProductListDiagnoser Example

```csharp
public class ProductListDiagnoser : IEmptyResultDiagnoser
{
    public string OperationName => "getProducts";
    private readonly ZnodePublish_Entities _publishDb;
    private readonly Znode_Entities _db;

    public async Task<DiagnosisReport> DiagnoseAsync(JsonElement args, object? result, CancellationToken ct)
    {
        var portalId = args.GetProperty("portalId").GetInt32();
        var categoryId = args.TryGetProperty("categoryId", out var c) ? c.GetInt32() : (int?)null;
        var report = new DiagnosisReport { Summary = "Product list is empty." };

        // 1. Portal exists?
        var portal = await _db.ZnodePortals.AsNoTracking()
            .Where(p => p.PortalId == portalId).Select(p => new { p.IsActive, p.StoreCode })
            .FirstOrDefaultAsync(ct);

        if (portal is null) {
            report.Checks.Add(new() { Name = "PortalExists", Status = Fail,
                Message = $"Portal {portalId} does not exist",
                Query = $"SELECT * FROM ZnodePortal WHERE PortalId = {portalId}" });
            return report;
        }
        report.Checks.Add(new() { Name = "PortalExists", Status = Pass,
            Message = $"Portal {portalId} ({portal.StoreCode}) exists" });

        // 2. Catalogs assigned?
        var catalogIds = await _db.ZnodePortalCatalogs.AsNoTracking()
            .Where(pc => pc.PortalId == portalId)
            .Select(pc => pc.PublishCatalogId).ToListAsync(ct);

        if (catalogIds.Count == 0) {
            report.Checks.Add(new() { Name = "PortalHasCatalog", Status = Fail,
                Message = $"No catalog assigned to portal {portalId}",
                Hint = "Assign a catalog in admin → Portal Settings" });
            return report;
        }
        report.Checks.Add(new() { Name = "PortalHasCatalog", Status = Pass,
            Message = $"Portal {portalId} uses catalog(s): {string.Join(",", catalogIds)}" });

        // 3. Category in catalog?
        if (categoryId.HasValue) {
            var catalogsWithCategory = await _db.ZnodePimCategoryHierarchies.AsNoTracking()
                .Where(ch => ch.CategoryId == categoryId.Value)
                .Select(ch => ch.PimCatalogId).ToListAsync(ct);

            var inRightCatalog = catalogsWithCategory.Any(c => catalogIds.Contains(c));
            if (!inRightCatalog) {
                report.Checks.Add(new() { Name = "CategoryInCatalog", Status = Fail,
                    Message = $"Category {categoryId} is NOT in portal's catalogs ({string.Join(",", catalogIds)})",
                    Hint = catalogsWithCategory.Count > 0
                        ? $"Category exists in catalog(s) {string.Join(",", catalogsWithCategory)}"
                        : "Category is not mapped to any catalog",
                    Query = $"SELECT PimCatalogId FROM ZnodePimCategoryHierarchy WHERE CategoryId = {categoryId}" });
                report.Suggestions.Add($"Map category {categoryId} to one of portal {portalId}'s catalogs");
                return report;
            }
            report.Checks.Add(new() { Name = "CategoryInCatalog", Status = Pass,
                Message = $"Category {categoryId} is mapped to portal {portalId}'s catalog" });
        }

        // 4. Last publish?
        var lastPublish = await _publishDb.ZnodePublishProductEntities.AsNoTracking()
            .Where(p => p.PortalId == portalId)
            .MaxAsync(p => (DateTime?)p.PublishTimeStamp, ct);

        if (lastPublish is null || lastPublish < DateTime.UtcNow.AddDays(-7)) {
            report.Checks.Add(new() { Name = "RecentPublish", Status = Warn,
                Message = lastPublish is null
                    ? "No products ever published for this portal"
                    : $"Last publish was {(DateTime.UtcNow - lastPublish.Value).Days} days ago",
                Hint = "Trigger a publish in admin → Product → Publish" });
        } else {
            report.Checks.Add(new() { Name = "RecentPublish", Status = Pass,
                Message = $"Last publish: {lastPublish:yyyy-MM-dd HH:mm}" });
        }

        // 5. Any products in published store for this portal/category?
        var rowCount = await _publishDb.ZnodePublishCategoryProductEntities.AsNoTracking()
            .CountAsync(cp => cp.PortalId == portalId
                && (categoryId == null || cp.CategoryId == categoryId), ct);

        report.Checks.Add(new() { Name = "PublishedProducts", Status = rowCount > 0 ? Pass : Fail,
            Message = $"ZnodePublishCategoryProductEntity has {rowCount} rows for portal {portalId}"
                + (categoryId.HasValue ? $", category {categoryId}" : ""),
            Query = $"SELECT COUNT(*) FROM ZnodePublishCategoryProductEntity WHERE PortalId = {portalId}"
                + (categoryId.HasValue ? $" AND CategoryId = {categoryId}" : "") });

        return report;
    }
}
```

## Pending Implementation

- [ ] `IDiagnosticRunner` + implementation
- [ ] `diagnose(operation, args)` query on `AdminQuery`
- [ ] `IEmptyResultDiagnoser` interface + base class
- [ ] `ProductListDiagnoser`, `CategoryDiagnoser`, `CartDiagnoser`, `OrderDiagnoser`, `PageBuilderDiagnoser`, `WebsiteEntryDiagnoser`
- [ ] `DiagnosticResult` GraphQL type
- [ ] Operation dispatcher (invoke target operation by name with args)
