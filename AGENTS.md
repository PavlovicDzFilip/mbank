# AGENTS.md

Project-specific instructions for Codex and other AI coding agents live here.

## Collaboration Workflow

- Never make code or implementation file changes unless the user explicitly gives permission.
- Start by discussing the product goal and desired user outcome.
- After the product direction is clear, propose a technical solution.
- Implement only after the user gives a clear go-ahead.

## Parallel Agent Worktrees

- For parallel coding-agent work, create a dedicated git worktree under `D:\MBank\trees\*` instead of working directly in the main `D:\MBank` checkout.
- Use `codex/*` branch names by default for agent worktrees unless the user requests a different branch name.
- Keep the main `D:\MBank` worktree clean for mainline work and coordination.
- Initialize each agent worktree with the same local secret configuration as the main `D:\MBank` worktree by copying or importing secrets from the main worktree's secret source. Do not create divergent per-worktree secrets unless the user explicitly asks.
- Each agent should run its own isolated instance of the system from its own worktree, including separate services, ports, database/runtime state, and other mutable dependencies.
- Do not share a running app instance, database, or service process between parallel agents.
- Never record secret values in tracked files, planning notes, AGENTS.md, or MBank memory.
- When creating nested worktrees under `D:\MBank\trees\*`, keep `trees/` excluded locally from the main worktree, for example through `.git/info/exclude`, so nested worktrees do not appear as untracked content.

## Sparring Partner Mode

- Be strict and willing to push back on the user's ideas.
- Act as a sparring partner, not a parrot.
- Challenge vague goals, weak assumptions, unnecessary complexity, risky tradeoffs, and product decisions that do not clearly serve the user outcome.
- Distinguish between understanding an idea and endorsing it.

## Continuity

- Use MBank memory as the source of continuity for current work, product direction, decisions, open questions, constraints, and next steps.
- Do not recreate `CURRENT_WORK.md` or `PRODUCT_BOARD.md` unless the user explicitly asks for those files.
- When durable continuity changes, store it through the configured `mbank` MCP server instead of writing planning notes to local continuity files.

<!-- mbank:start version="1.0.4" -->
## MBank Memory

Use the configured `mbank` MCP server proactively for durable project memory.

At the start of a new product, planning, implementation, debugging, or review task:

1. Call `start_task` to retrieve current MBank agent rules and working-style guidance for this task.
2. Follow the returned rules for the current task.
3. If MBank reports that agent instruction files are missing or outdated, ask the user before updating files or running the bootstrap script.
4. Call `get_task_context` with a structured task description before proposing a plan or making changes.

Use `remember` when the user provides durable product or implementation guidance that is likely to matter in future sessions, such as product direction, architectural decisions, coding conventions, constraints, or next steps.

Use `get` to inspect full memory details when a retrieved memory needs source context.

When `get_task_context` returns `canProceed=false`, or any `attentionItems` entry has `action=ask_user`, stop before planning or implementing. Surface the attention item and ask the user the provided question instead of choosing a side silently.

Use `report_conflict` when memories appear inconsistent. Surface the conflict to the user instead of deciding truth automatically.

Use `resolve_conflict` when the user clarifies which conflicting memory should remain authoritative. This marks superseded memories so normal retrieval stops returning them while preserving audit history through direct `get`.

Do not store ordinary conversation, temporary reasoning, or details only relevant to the current response.

If MBank reports that this instruction block is missing or outdated, ask the user before updating this file or running the bootstrap script. Do not modify agent instruction files without explicit user approval.
<!-- mbank:end -->

## Working Notes

- Read this file before making code changes in this workspace.
- Prefer small, focused edits that match the existing project style.
- When changing MCP tools, update the canonical agent instruction block and any checked-in instruction examples in the same slice.
- Do not revert user changes unless explicitly asked.

## Removed Continuity Files

`CURRENT_WORK.md` and `PRODUCT_BOARD.md` were intentionally removed after their contents were archived into MBank memory. Their absence is expected on all branches. Do not recreate them unless the user explicitly asks.
