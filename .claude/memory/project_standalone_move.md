---
name: Standalone Project Decision
description: GraphQL project was moved from inside API repo to standalone project at D:\Base_Code\Znode.Engine.GraphQL\ for clean maintenance
type: project
---

Originally the GraphQL project lived inside the API repo at `znode10-api-migration/Znode.Multifront/Znode.Engine.GraphQL/`. User requested moving it to a standalone project at `D:\Base_Code\Znode.Engine.GraphQL\`.

**Why:** Clean separation from the main API. The embedded location caused conflicts (e.g., `ExceptionHandlingMiddleware` from the parent API). Standalone project is easier to maintain, deploy independently, and reason about.

**How to apply:** The project uses `Microsoft.NET.Sdk.Web` (not class library). It has its own `Program.cs`, `appsettings.json`, and runs on `https://localhost:44376`. It references `Znode.Libraries.Data` via project reference for DbContext access. Never move it back into the API repo.
