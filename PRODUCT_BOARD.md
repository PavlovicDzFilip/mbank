# Product Board

Shared planning board for what we want to achieve.

## Product Goal

- Build an MCP-first memory system for product and coding agents used by dev teams.
- Help agents preserve decision context, product rationale, coding conventions, tradeoffs, and related historical knowledge across sessions.
- Make product knowledge available to coding agents so implementation work is informed by feature intent, business rationale, and prior decisions.

## Target Users

- Dev teams using Codex, Claude Code, Cursor, or similar coding/product agents.
- Initial v1 user is the project owner personally; the system should work very well for one developer across their own agent workflows.
- Dev teams are the next expansion target once the personal workflow is proven.
- Larger company/team expansion is a later opportunity, not the v1 wedge.

## User Problems

- Agents lose context between sessions, causing repeated explanations, avoidable mistakes, and inconsistent decisions.
- Product decisions, tradeoffs, and business reasons are often buried in conversations, tickets, meetings, or people's heads.
- Coding agents can implement features without understanding why the feature exists or why earlier choices were made.
- Teams often lack ADRs or maintain them inconsistently; useful historical reasoning is scattered across Jira tickets, chats, refinement notes, and code.

## Planned Outcomes

- Agents can store memories through an MCP server while working.
- Agents can ask for task-specific context through MCP before planning or implementation.
- Product agents can record decisions, reasons, tradeoffs, impacts, and related context.
- Coding agents can retrieve product rationale plus technical conventions and implementation history.
- Memories can be scoped initially around a repository or project identity, with monorepo handling deferred.
- Retrieval may support text search, tags, and structured filters.
- Agents ask the backend for relevant context for a specific task; the backend performs search/ranking and returns relevant results.

## Scope

- MCP-first infrastructure for agent store and retrieval operations.
- No UI in the initial MVP; focus first on MCP storage and retrieval.
- Simple ASP.NET Core Razor Pages web UI may be added after MCP storage/retrieval works; no SPA for v1.
- MCP server for agent store and retrieval operations.
- No separate public API is required for v1 unless needed by the MCP server or Razor Pages internals.
- No project scope in the initial personal MVP.
- Later, each MCP server configuration may be scoped to one project/workspace; the agent does not need to pass repo URL on every call.
- Later, authentication or API keys should map agents/users to projects, teams, and companies.
- Company/team/user scoping, management, security, and enterprise controls remain later design dimensions.
- Long-term ambition: replace or supplement ADRs, Jira history, refinement notes, and unwritten team knowledge, but v1 must stay narrower.

## Priorities

- Define the first demo workflow around a product task becoming implementation work.
- Focus MVP on storage and retrieval quality through MCP.
- Decide what a "memory" is and what metadata it requires.
- Decide how agents should know when to store versus retrieve memory.
- Decide how to make MCP tool usage reliable enough across Codex, Claude Code, and Cursor.
- Defer final scope model, especially monorepo handling.

## Decisions

- Product discussion comes before technical design.
- Implementation happens only after explicit go-ahead.
- V1 is aimed at dev teams using coding/product agents, not broad enterprise knowledge management.
- Product should be MCP-first; the web UI should be simple.
- Backend preference is C#.
- Frontend choice for v1 is Razor Pages server-side rendering.
- Local environment has .NET 9/10 SDKs and Docker available; `psql` is not currently on PATH.
- Docker Compose with Postgres plus pgvector is required so the system is runnable from day one.
- V1 MCP tool surface should stay small: `remember`, `get_task_context`, `get`, and `report_conflict`.
- Do not create a separate `find_related` MCP tool for v1; related memories should be returned as links on memory detail/results.
- Avoid exposing generic `search` as a core v1 workflow unless a clear manual/debug use case appears; `get_task_context` should perform search/ranking based on task data provided by the agent.
- Initial MVP has no project scoping.
- Later project scoping should use MCP/project configuration first, then auth/API-key mapping for team/company use.
- Initial embedding model is OpenAI `text-embedding-3-small` via Microsoft .NET/Agent tooling abstractions, stored in Postgres pgvector.
- Initial MCP transport should be Streamable HTTP inside the ASP.NET app.

## Product Tiers

- Tier 1: Personal use. Make the memory system work very well for the project owner with minimal management/security overhead.
- Tier 2: Dev-team use. Add project/team management, shared visibility, and collaboration workflows.
- Tier 3: Company/enterprise use. Add security, permissions, auditability, tenant administration, and broader knowledge-management integrations.

## Memory Model Hypotheses

- Memory types may include facts, decisions, preferences, related context, and history.
- Memories should include source, timestamp, confidence, links to conversations, and links to related memories.
- Memory conflicts must be handled later, likely through conflict detection and user confirmation when the system cannot resolve disagreement.
- Memories should be short, atomic entries linked to a longer source/raw record.
- Store as much backend evidence/source data as practical so it can be reused later.
- Users should be able to edit memories once a web UI exists.
- Capture should generally happen automatically when users provide durable product or implementation guidance.
- Memories are active immediately in the personal MVP; do not require manual review for every memory.
- Agents may need to ask clarifying questions when provided guidance is ambiguous or conflicts with existing memories.
- Memory subjects should depend on the task: business features attach to product capabilities; technical changes attach to technical areas such as modules, endpoints, jobs, conventions, or architecture.
- Tool descriptions should emphasize durable guidance so agents do not send every utterance as memory.
- A future ingestion model may use agent hooks to send broad session data and let the backend decide what is valuable, but that is out of scope for v1.
- `remember` should accept multiple atomic memories in one call.
- `remember` should instruct agents to provide source/raw context when available without bloating normal task context.
- For v1, `remember` trusts the agent to send already-atomic memories; backend LLM extraction/splitting is a later revisit.
- `get_task_context` should return strong and possible memory matches plus clarifying questions.
- Light conflict detection is acceptable in v1 if cheap to implement.
- `get_task_context` should distinguish strong query matches from possible query matches instead of returning all candidates as equally relevant context.
- Query match responses should use clear names: `memoryConfidence` for stored-memory trust, `matchScore` for normalized query-match strength, `matchStrength` for the bucket, and `matchReason` for ranking evidence.
- `matchScore` is bucket-normalized for agent readability, not a probability. Agents should reason primarily from `matchStrength`.

## Risks / Pushback

- A generic memory dump will likely create noisy retrieval and lower trust.
- Memory scope needs to be explicit early; company/team/user boundaries affect the whole architecture.
- The product must distinguish durable facts, temporary session notes, user preferences, and decisions.
- Automatic memory creation risks storing hallucinations, duplicates, and low-value noise.
- Agent reliability is not guaranteed; MCP tools must be designed so agents know when and how to store and retrieve context.
- Replacing ADRs/Jira history/informal team knowledge is valuable, but too broad unless v1 focuses on a narrow task-to-implementation path.
- `remember` being too generic may reduce memory quality unless it accepts enough structure or the backend normalizes entries.
- Asking clarifying questions "via MCP" likely means the MCP tool returns a response instructing the agent to ask the user, not that MCP asks directly.
- Generic search may be unnecessary or harmful in v1 if it encourages agents to perform unstructured retrieval instead of asking for task context.

## Demo Candidate

- A product manager creates a new task.
- A developer provides additional task context.
- The agent retrieves relevant product and technical memories.
- The agent asks clarifying questions when context is missing or conflicting.
- The agent stores the new information and proceeds toward implementation after approval.
