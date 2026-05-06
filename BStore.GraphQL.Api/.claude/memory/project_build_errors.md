---
name: Known Build Issues & Fixes
description: Common compilation errors encountered and their fixes — ambiguous types, deprecated APIs, locked files, missing packages
type: project
---

**Recurring issues and fixes:**

1. **`AddAuthorization()` ambiguity (CS1929)** — Need `HotChocolate.AspNetCore.Authorization` package. Without it, `AddAuthorization()` on `IRequestExecutorBuilder` fails.

2. **`SetPagingOptions` deprecated (CS0618)** — Replace with `ModifyPagingOptions(opt => { ... })`.

3. **`AllowIntrospection` deprecated (CS0618)** — Still works but warns. HotChocolate 14 prefers `DisableIntrospection`. Currently suppressed.

4. **File locked by running process (MSB3021)** — Kill before build: `taskkill //f //im "Znode.Engine.GraphQL.exe"`

5. **`KeyNotFoundException` ambiguity (CS0104)** — Conflict between `GreenDonut.KeyNotFoundException` and `System.Collections.Generic.KeyNotFoundException`. Fix: fully qualify as `System.Collections.Generic.KeyNotFoundException`.

6. **`ZnodeErrorFilter` not found (CS0246)** — Missing `using Znode.Engine.GraphQL.Schema;` in `GraphQLServiceRegistration.cs`.

7. **Missing `ZnodePublish_Entities` namespace (CS0246)** — Need `using Znode.Libraries.Data.ZnodeEntity;` and `using Znode.Libraries.Data.PublishDataModel;`.

8. **`or` pattern syntax (CS1026)** — `x.ValueKind == JsonValueKind.True or JsonValueKind.False` is invalid C#. Fix: `(x.ValueKind == JsonValueKind.True || x.ValueKind == JsonValueKind.False)`.

9. **XML entity reference in doc comments (CS1570)** — `{portalId}` in XML comments treated as XML entity. Suppressed via `<NoWarn>1591</NoWarn>` in csproj.

**How to apply:** When build fails, check this list first before investigating. Most errors are recurring patterns.
