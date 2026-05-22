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

<!-- mbank:start version="1.0.8" -->
## MBank Memory

Use the configured `mbank` MCP server proactively for durable project memory.

At the start of a new product, planning, implementation, debugging, or review task:

1. Call `start_task` to retrieve current MBank agent rules and working-style guidance for this task.
2. Follow the returned rules for the current task.
3. If MBank reports that agent instruction files are missing or outdated, ask the user before updating files or running the bootstrap script.
4. Call `get_task_context` with a structured task description for the current focus (title, description, kind, and optional work area).

Re-call `get_task_context` when conversation focus changes before answering substantive project-specific questions. Call again when the user shifts topic, product area, or technical subject; when moving between phases such as exploration, planning, implementation, or debugging; or when the user references prior decisions or constraints you have not yet retrieved for this focus. Skip re-calls for trivial same-focus follow-ups.

Before proposing a plan, making changes, or answering substantive project-specific questions (including exploration), use memories from the latest `get_task_context` result for the current focus.

Use `remember` when the user provides durable product or implementation guidance, or after the user agrees to store items you offered from exploration. Do not call `remember` without approval except when the user explicitly asks to store something.

## Offering to capture memory

After substantive exploration (ideas, direction, tradeoffs, or what is possible):

1. Answer using `get_task_context` and the conversation.
2. If durable candidates may matter in future sessions but were not explicitly committed, offer to store them in MBank. List one to three atomic candidates with suggested types (use `captureHint` from `describe_memory_types`).
3. Call `remember` only after the user agrees or explicitly asks to store something.
4. Do not store rejected options, agent speculation the user did not adopt, or ordinary chat.

Suggested types for offer candidates: `note` for vague ideas; `product-open-question` or `technical-open-question` for unresolved options; `product-strategy` or `product-reasoning` for tentative direction; `product-decision` or `technical-decision` only when the user has clearly chosen.

Skip the offer when nothing is plausibly durable or the thread was purely meta.

When `get_task_context` returns a `capture_offer` attention item, follow it after answering.

Use `describe_memory_types` when you need memory types, `captureHint` guidance, or `captureOfferPolicy` before offering or calling `remember`.

When calling `remember`, set each memory type to one of these valid MemoryType values: `product-strategy`, `product-decision`, `product-reasoning`, `product-requirement`, `product-constraint`, `product-risk`, `product-open-question`, `technical-architecture`, `technical-decision`, `technical-reasoning`, `technical-rule`, `technical-convention`, `technical-preference`, `technical-constraint`, `technical-risk`, `technical-open-question`, `implementation-result`, `validation-result`, `operational-status`, `operational-procedure`, `agent-instruction`, `project-policy`, `user-preference`, `collaboration-preference`, `note`. Do not invent new type values.

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
