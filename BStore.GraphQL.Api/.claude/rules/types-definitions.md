---
paths:
  - "**/Types/**/*.cs"
---

# GraphQL Type Definition Rules

## Structure

- Types are **plain C# POCOs** — no logic, no constructors with parameters.
- Group related types in a single file per domain (e.g., `OrderType.cs` contains `OrderType`, `OrderLineItemType`, etc.)
- Input types go in the **same file** as their corresponding output type.
- Use `[GraphQLDescription("...")]` for field documentation visible in schema introspection.

## Nullability & Defaults

- All **nullable** reference properties: `string? Name`
- All **non-nullable** strings default to `= string.Empty`
- All **collections** default to `new()` — never leave them null: `public List<T> Items { get; set; } = new();`
- Price/money fields use `decimal`, never `double` or `float`
- Non-nullable complex child types default to `= new()`:
  ```csharp
  public SeoType Seo { get; set; } = new();
  ```

## [GraphQLName] — REQUIRED for All Acronym Properties (ADR-006)

HotChocolate camelCase conversion does not fully lowercase acronyms. `B2BContext` becomes `b2BContext` in the schema (wrong), not `b2bContext`. **This breaks frontend field access.**

**Every property containing an acronym must use `[GraphQLName]`:**

```csharp
// ✅ Correct
[GraphQLName("b2bContext")]
public B2BContextType B2BContext { get; set; } = new();

[GraphQLName("seoUrl")]
public string SEOUrl { get; set; } = string.Empty;

[GraphQLName("pimCatalogId")]
public int PIMCatalogId { get; set; }
```

**Affected acronyms:** `B2B`, `B2C`, `PIM`, `OMS`, `CMS`, `SEO`, `URL`, `SKU`, `ERP`, `API`, `CDN`, `JWT`, `ID` (when in mid-word, e.g., `OrderID` → `[GraphQLName("orderId")]`)

## Authorization

- **NEVER** add `[Authorize]` to types — authorization goes on resolvers only.

## Registration

- When adding a new type, register it in `GraphQLServiceRegistration.cs` if HotChocolate cannot auto-discover it.
- Update the Types section in `CLAUDE.md` when adding a new `*Types.cs` file.
