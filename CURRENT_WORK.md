# Current Work

Detailed continuity notes for the active work session live here.

## Current Focus

- Validation checkpoint after proving the MVP MCP memory loop works end to end.
- 2026-05-10 readiness check: Debug build passed, Debug test suite passed, Release test suite passed, and the git worktree was clean afterward.
- 2026-05-10 local deployment: latest Release publish was copied to `MBank-local\app`, local database migrations were already up to date, Docker containers `mbank-local-api-1` and `mbank-local-postgres-1` were running, root endpoint returned running status, `/mcp` advertised `remember`, `get_task_context`, `get`, and `report_conflict`, and `MBank-local\.env` had `OPENAI_API_KEY` set.

## Decisions Made

- Product direction should be discussed before technical implementation.
- Technical solutions should be proposed before implementation.
- Code and implementation file changes require explicit user permission.
- `CURRENT_WORK.md` and `PRODUCT_BOARD.md` may be updated when useful for continuity and planning.
- Codex should act as a strict sparring partner and push back on weak ideas.
- V1 wedge is dev teams using product and coding agents; company-wide memory is deferred.
- The system should connect product rationale to coding-agent implementation context.
- Backend preference is C#; UI should be as simple as practical.
- Frontend decision: Razor Pages server-side rendering for v1; no SPA.
- Initial MVP has no UI; focus on MCP storage and retrieval first.
- Later product surface: MCP server plus simple web app. A separate public API is not needed unless required internally.
- Long-term ambition includes replacing/supplementing ADRs, Jira history, refinement notes, and unwritten team knowledge, but v1 remains narrower.
- V1 MCP tools under consideration: `remember`, `get_task_context`, `get`, and `report_conflict`.
- Generic `search` is not part of the core v1 workflow unless a clear manual/debug use case appears.
- Memory subjects depend on the work: product capabilities for business features, technical areas for implementation/architecture work.
- Future idea: hooks could send broader session data to the backend for value extraction, but this is not v1.
- V1 should work very well for the project owner personally before expanding to teams or companies.
- Initial MVP has no project scope; later MCP/project configuration can provide project scope, and later auth/API keys can map callers to projects, teams, and companies.
- MVP focus is storage and retrieval quality through MCP.
- Local environment has .NET 9/10 SDKs and Docker available; `psql` is not currently on PATH.
- Docker Compose with Postgres plus pgvector is required so the system is runnable from day one.
- Memories should be active immediately for personal use, not manually reviewed one by one.
- `remember` should accept multiple atomic memories and request source/raw context where available.
- `get_task_context` should return clarifying questions.
- Light conflict detection is acceptable if cheap.
- Initial embedding model is OpenAI `text-embedding-3-small` via Microsoft .NET/Agent tooling abstractions, stored in Postgres pgvector.
- Initial MCP transport should be Streamable HTTP inside the ASP.NET app.
- For v1, `remember` trusts the agent to send already-atomic memories; backend LLM extraction/splitting is a later revisit.
- User explicitly wants EF Core for persistence.
- User wants DDD-style entities without lazy loading or navigation references; entities should hold foreign key IDs and services/queries should load related data explicitly.
- User wants explicit OpenAI configuration via `OpenAI:ApiKey`; do not read `OPENAI_API_KEY` directly. Validate required configuration at startup, not when a tool is invoked.
- Local `OpenAI:ApiKey` is configured via .NET user-secrets for `MBank.Api`; do not record the secret value in repo files or planning notes.
- When more team members use the system, move secrets such as `OpenAI:ApiKey` to Azure Key Vault or an equivalent managed secret store.
- When Codex runs the MBank project, it should start the app in a visible window so the user can see and control the running process.
- When Codex runs `MBank.Api`, it must use `D:\MBank\src\MBank.Api` as the process working directory so appsettings and .NET user-secrets are applied correctly.
- Scaffolded ASP.NET Core 10 app with MCP endpoint, EF Core entities, pgvector migration, Docker Compose Postgres, and OpenAI embeddings.
- Docker Compose/Postgres/pgvector became available later in the session; migrations were applied successfully to the local database.
- Retrieval has been updated to true hybrid retrieval: independent pgvector cosine candidates plus PostgreSQL full-text candidates over a generated `SearchVector`, merged with reciprocal rank fusion.
- MCP validation passed on 2026-05-08: `get_task_context` returned relevant memories with RRF/vector/full-text-related relevance signals, `remember` stored a validation memory, `get` fetched it with source evidence, and a differently worded `get_task_context` retrieved the new memory as the top result.
- Fixed a full-text query translation bug by inlining `EF.Functions.WebSearchToTsQuery(...)` inside the EF query instead of storing it in a local variable that forced client evaluation.
- Fixed noisy context conflict detection so same-subject groups are compared within the group, not against every scored memory.
- Branch `codex/retrieval-quality-tests` adds normal xUnit tests under `tests/MBank.Tests` using `WebApplicationFactory<Program>`, DI overrides for fake deterministic embeddings, and Testcontainers with the `pgvector/pgvector:pg16` image.
- Retrieval quality tests cover top-result ranking, launch guidance retrieval, full-text signal visibility, warning/conflict scoping, cross-subject conflict suppression, and `get` source evidence.
- Readiness judgment on 2026-05-10: the MCP-first MVP behavior is proven well enough to move to the next phase, provided "next phase" does not mean enterprise/team/company scope yet.
- Retrieval quality tests exposed that `WebSearchToTsQuery` over generated multi-field task text was too strict for candidate generation; full-text retrieval now builds an OR-style `ToTsQuery` from extracted terms.
- Project knowledge was stored into MBank itself as atomic memories.
- `AGENTS.md` now instructs future agents to call the configured `mbank` MCP server proactively.
- `get_task_context` now returns `strongMatches` and `possibleMatches` instead of `relevantMemories`.
- Query match items now expose `memoryConfidence`, normalized bucketed `matchScore`, `matchStrength`, and `matchReason` so agents do not confuse stored-memory trust with query-match strength.
- Match score normalization uses product buckets: possible matches map into a lower normalized range and strong matches map into the upper normalized range; the normalized value is not a probability.

## Open Questions

- What is the narrowest v1 workflow that proves value: product task intake, implementation planning, or coding execution?
- What minimum memory schema is needed for facts, decisions, preferences, related context, and history?
- How much automatic memory creation is acceptable before user review becomes necessary?
- How should MCP tools be designed so agents reliably call them before planning and after receiving durable guidance?
- How should the backend decide what context is relevant for a specific implementation task?
- How should `remember` be structured so a single tool can still produce high-quality memories?
- Should generic search exist at all in v1, or only later as a human/admin/debug feature?
- Confirm Codex UI add-server behavior. The running MCP server works for tools, but Codex UI probed `resources/templates/list`; adding empty resource/template handlers may be needed if the UI still fails.
- Restart the running MBank.Api process before testing the new hybrid retrieval code through MCP, because the old server process was still locking the Debug build output.

## Next Steps

- Consider adding MCP JSON-RPC endpoint-level tests later if direct HTTP contract coverage becomes important.
- If Codex UI still fails to add the server, implement empty MCP resource and resource-template list handlers.
- Consider adding a memory listing/admin capability later; currently memory count/listing requires direct database query.
- Calibrate the `possible` and `strong` raw-score thresholds after collecting more real retrieval examples.
- Next recommended phase: add the smallest operator-facing visibility surface, likely a simple Razor Pages memory list/detail view, while keeping MCP storage and retrieval as the product core.
- Before choosing the next build phase, dogfood MBank through real single-user workflows: cross-session recall, correction/staleness, duplicate/noise behavior, and agent compliance with retrieve-before-plan/store-durable-guidance habits.
