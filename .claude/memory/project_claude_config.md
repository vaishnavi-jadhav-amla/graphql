---
name: Claude Code Configuration
description: Full Claude Code setup in .claude/ directory — 6 sub-agents, 6 path-specific rules, settings, and memory for the GraphQL project
type: project
---

**Sub-agents (`.claude/agents/`):**
- `db-expert` (Sonnet) — EF Core queries, schema analysis, SQL optimization
- `code-reviewer` (Sonnet) — Security, performance, pattern adherence reviews
- `api-tester` (Sonnet) — Builds server, sends curl requests, validates responses
- `architecture` (Opus) — Designs new features, plans modules, ensures consistency
- `explorer` (Haiku) — Searches all 4 Znode repos for existing patterns/logic
- `build-runner` (Haiku) — Compiles, fixes build errors

**Path-specific rules (`.claude/rules/`):**
- `graphql-resolvers.md` — Triggers on `Queries/**`, `Mutations/**`, `Schema/**`
- `database-efcore.md` — Triggers on `Services/**`, `*DbContext*`, `*Entity*`
- `types-definitions.md` — Triggers on `Types/**`
- `pipeline-interceptors.md` — Triggers on `Pipeline/**`, `Interceptors/**`
- `providers.md` — Triggers on `Providers/**`
- `services.md` — Triggers on `Services/**`

**Settings (`.claude/settings.json`):** Auto-allows dotnet/git commands, sets Development env.

**How to apply:** When adding new features, the relevant rules auto-load based on file paths being edited. Use sub-agents for specialized tasks (db queries → db-expert, code review → code-reviewer, etc.).
