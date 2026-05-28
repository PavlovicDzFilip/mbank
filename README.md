# MBank

MCP-first memory storage and retrieval for AI agents.

## Current MVP

- ASP.NET Core 10 MCP server.
- Streamable HTTP MCP endpoint at `/mcp`.
- EF Core persistence with Postgres and pgvector.
- OpenAI `text-embedding-3-small` embeddings.
- Magic-link login at `/login`.
- Signed-in project workspace at `/projects` for users, projects, and project tokens.

## Prerequisites

- .NET SDK **10.0.103** exactly (`global.json` pins this version for local dev, CI, and Docker). Install with `dotnet-install.sh --version 10.0.103`, or use a Fedora/RHEL package that ships SDK 10.0.103.
- Docker Desktop running with the Linux engine enabled.
- Local secrets configured with .NET user secrets for the project you run.

## Run Locally With Aspire

Aspire is the local runtime for this project checkout. It runs the AppHost, Postgres, migrations, the API, Mailpit, and ports from the local project path.

Configure the AppHost secrets once for the local project checkout:

```powershell
dotnet user-secrets --project src\MBank.AppHost\MBank.AppHost.csproj set "Parameters:openaiApiKey" "<openai-api-key>"
dotnet user-secrets --project src\MBank.AppHost\MBank.AppHost.csproj set "Parameters:postgresPassword" "<postgres-password>"
```

Start the local runtime:

```powershell
dotnet run --project src\MBank.AppHost\MBank.AppHost.csproj
```

The AppHost starts the Aspire dashboard, applies EF Core migrations through the `migrate` resource, then starts the `api` resource. Development magic-link email goes to the `mailpit` resource.

Production deployment does not use the local AppHost. Merging to `master` runs GitHub Actions (test, lint, build, deploy to Hetzner). Use `scripts\Deploy-MBankHetzner.ps1` only as an emergency fallback if Actions is unavailable.

### Direct API Runs

Aspire normally supplies API configuration. If you run `MBank.Api` directly from the local project checkout, configure the API project secrets:

```powershell
dotnet user-secrets --project src\MBank.Api\MBank.Api.csproj set "OpenAI:ApiKey" "<openai-api-key>"
dotnet user-secrets --project src\MBank.Api\MBank.Api.csproj set "ConnectionStrings:MemoryDb" "Host=localhost;Port=5432;Database=mbank;Username=mbank;Password=<postgres-password>"
dotnet user-secrets --project src\MBank.Api\MBank.Api.csproj set "Smtp:Host" "<smtp-host>"
dotnet user-secrets --project src\MBank.Api\MBank.Api.csproj set "Smtp:Port" "<smtp-port>"
dotnet user-secrets --project src\MBank.Api\MBank.Api.csproj set "Smtp:FromAddress" "<from-address>"
```

Use a Postgres password and SMTP settings that match your local runtime.

## Production deploy (GitHub Actions)

Every pull request and merge-queue run executes **CI** (restore, `dotnet format`, Release build with analyzers, tests, and a Docker build of both `api` and `migrate` images). Pushing to `master` after a merge runs **Deploy production**: the same checks, then Docker images tagged with UTC `yyyyMMddHHmmss`, registry push, SSH deploy in `/vault`, migrate, recreate `mbank-api`, and a smoke `curl` against `/onboarding/agent-instructions`.

Require the **CI** check on `master` branch protection (or merge queue) so pull requests cannot merge when the Docker build fails.

### One-time GitHub setup

Repository secrets:

| Secret | Purpose |
| --- | --- |
| `REGISTRY_USERNAME` | Push to `registry.engineeringwithfilip.com` |
| `REGISTRY_PASSWORD` | Registry credentials |
| `SSH_PRIVATE_KEY` | SSH key for production deploy |

Enable a **merge queue** on `master` (branch protection) so when another PR merges while yours is queued, GitHub resets the queue entry and re-runs CI on the updated base. Without a merge queue, update your PR branch after `master` moves so CI runs on the latest code.

### Server layout

- Compose and `mbank.env` live on the host under `/vault` (not in this repo).
- `mbank.env` shape: see `docs/mbank.env.example`.
- Deploy sets `MBANK_VERSION` to the image tag; compose should reference `${MBANK_VERSION}` for `mbank/api` and `mbank/migrate`.

### Verify or recover

- **Actions:** check the latest *Deploy production* workflow run.
- **On server:** `docker compose ps` in `/vault`; migrate logs from the last `mbank-migrate` run.
- **Failed migration:** fix forward in code, merge to `master`, redeploy; do not recreate `mbank-api` until migrate succeeds (the workflow stops on migrate failure).
- **Rollback (manual):** SSH to `/vault`, set `MBANK_VERSION` to a previous datetime tag, then `docker compose pull`, run migrate, recreate `mbank-api`.
- **Rotate app secrets:** edit `mbank.env`, then `docker compose up -d --no-deps --force-recreate mbank-api`.

### URLs, UI, and MCP

Aspire assigns HTTP ports dynamically. Do not assume a fixed port such as `localhost:5221`; use the URLs shown in the Aspire dashboard for the current run.

From the dashboard:

| Resource | Use for |
| --- | --- |
| `api` HTTP endpoint | MBank base URL (`/`, `/login`, `/projects`, `/mcp`) |
| `mailpit` HTTP endpoint | Reading magic-link sign-in emails in development |

Common paths on the `api` base URL:

- `/` redirects to `/login`, or to `/projects` when you are already signed in.
- `/login` is the magic-link sign-in screen.
- `/projects` is the signed-in project workspace.
- `/projects/{id}/memories` lists stored memories for a project (read-only).
- `/projects/{id}/memories/{memoryId}` shows full memory detail, source evidence, and links.
- `/mcp` is the MCP endpoint.

Signed-in project members can browse memories in the UI to verify what agents stored, inspect sources, and spot conflicts or superseded entries. Memory creation and conflict resolution remain MCP-only.

Example MCP client configuration (replace the host with the current `api` HTTP endpoint from the dashboard):

```toml
[mcp_servers.mbank]
enabled = true
url = "http://127.0.0.1:<api-port>/mcp"
bearer_token_env_var = "MBANK_PROJECT_TOKEN"
```

Mailpit's web UI and HTTP API are listed in the Aspire dashboard, which lets local automation verify that magic-link emails were received without mailbox credentials.

## Provisioning

Sign in at `/login`, then create projects and project tokens in `/projects`. Project tokens are returned once when created. Store only the returned token in client configuration; MBank stores its hash plus non-secret display prefix/suffix metadata.

Set `MBANK_PROJECT_TOKEN` to a project token created in the UI. `/mcp` rejects requests without a valid project token. Point your MCP client at `<api-base-url>/mcp` (see Aspire dashboard when running locally).

## MCP Tools

- `StartTask`: returns current MBank agent rules, working-style guidance, and instruction-file onboarding status for the current task.
- `Remember`: stores durable product or implementation guidance as one or more atomic memories.
- `GetTaskContext`: retrieves strong and possible memory matches for the current focus at task start and when conversation focus shifts (including exploration). May return a `capture_offer` attention item for planning or exploration tasks.
- `DescribeMemoryTypes`: returns memory types with `captureHint` guidance plus `captureOfferPolicy` for when to offer `remember` versus storing silently.
- `Get`: fetches one memory with source evidence and linked memories.
- `ReportConflict`: records that memories appear to conflict and should be clarified later.
- `ResolveConflict`: marks clarified losing memories as superseded so normal retrieval no longer returns them, while preserving audit history.
- `CheckOnboarding`: reports whether supported repo agent instruction files contain the current versioned MBank block.

## Agent Instruction Bootstrap

Adding the MCP server makes MBank available to an agent. For reliable behavior, each repo should also include a small MBank instruction block in the agent instruction file.

The bootstrap scripts fetch the canonical instruction block from a running MBank server. They do not carry a fallback copy of the instructions. If MBank is not running, bootstrap fails instead of writing stale guidance. The server does not know about Codex, Claude, Cursor, or file placement; the scripts handle that mapping locally.

The bootstrap scripts only add or update content between `<!-- mbank:start ... -->` and `<!-- mbank:end -->`. They prompt before changing files by default.

When the MBank instruction text changes, bump the canonical block version in the server so agents can detect stale repo instructions. When MCP tools change, update the canonical agent instructions in the same slice.

Public URL:

```bash
curl -fsSL https://mbank.engineeringwithfilip.com/bootstrap-agent.sh | sh
```

Windows PowerShell:

```powershell
Invoke-WebRequest https://mbank.engineeringwithfilip.com/bootstrap-agent.ps1 -OutFile bootstrap-agent.ps1
.\bootstrap-agent.ps1
```

By default, the public script updates all supported instruction targets:

- `AGENTS.md`
- `CLAUDE.md`
- `.cursor/rules/mbank.mdc`

Inspect-first usage:

```bash
curl -fsSL https://mbank.engineeringwithfilip.com/bootstrap-agent.sh -o bootstrap-agent.sh
less bootstrap-agent.sh
sh bootstrap-agent.sh
```

```powershell
Invoke-WebRequest https://mbank.engineeringwithfilip.com/bootstrap-agent.ps1 -OutFile bootstrap-agent.ps1
Get-Content .\bootstrap-agent.ps1
.\bootstrap-agent.ps1
```

Local development usage (use the `api` HTTP URL from the Aspire dashboard as `-ServerUrl` / `--server-url`):

```powershell
.\scripts\bootstrap-agent.ps1 -Client codex -ServerUrl http://127.0.0.1:<api-port>
```

```bash
sh scripts/bootstrap-agent.sh --client codex --server-url http://127.0.0.1:<api-port>
```

Supported clients:

- `codex`: updates `AGENTS.md`.
- `claude`: updates `CLAUDE.md`.
- `cursor`: updates `.cursor/rules/mbank.mdc`.
- `all`: updates all supported targets.

Noninteractive usage is available with `--yes` or `-Yes`, but repo instruction updates should still be explicitly approved by the user before an agent runs them.

Manual block:

```md
<!-- mbank:start version="1.0.10" -->
## MBank Memory

Use the configured `mbank` MCP server proactively for durable project memory.

At the start of a new product, planning, implementation, debugging, or review task:

1. Call `start_task` to retrieve current MBank agent rules and working-style guidance for this task.
2. Follow the returned rules for the current task.
3. Report the MBank agent instruction version from onboarding status. If MBank reports that agent instruction files are missing, outdated, or not checkable from the server, ask the user before updating files or running the bootstrap script.
4. Call `get_task_context` with a structured task description for the current focus (title, description, kind, and optional work area).

Re-call `get_task_context` when conversation focus changes before answering substantive project-specific questions. Call again when the user shifts topic, product area, or technical subject; when moving between phases such as exploration, planning, implementation, or debugging; when the user references prior decisions or constraints you have not yet retrieved for this focus; when you are uncertain how to proceed; before choosing between materially different technical approaches; or before specific implementation decisions that may depend on project history, prior decisions, constraints, or user preferences. Skip re-calls for trivial same-focus follow-ups.

Before proposing a plan, making changes, or answering substantive project-specific questions (including exploration), use memories from the latest `get_task_context` result for the current focus.

Use `remember` when the user provides durable product or implementation guidance, or after the user agrees to store items you offered from exploration. Do not call `remember` without approval except when the user explicitly asks to store something.

When the user asks why the agent chose one approach instead of another, treat it as a strong signal that an assumption, preference, or rule may have been missed. Pause and identify the possible correction before continuing. If the exchange reveals durable guidance, offer to store it in MBank, or call `remember` when the user explicitly asks to store it. Do not store the agent's guess as fact before the user confirms the underlying guidance.

For `technical-rule`, `technical-convention`, or `technical-preference`, consider mechanical enforcement (Roslyn analyzer, EditorConfig, linter, CI): recommend tooling first, then a short memory with the diagnostic or rule id.

## Offering to capture memory

After substantive exploration (ideas, direction, tradeoffs, or what is possible), offer to store durable candidates using `captureHint` types from `describe_memory_types`, then call `remember` only after the user agrees. When `get_task_context` returns a `capture_offer` attention item, follow it after answering.

Use `get` to inspect full memory details when a retrieved memory needs source context.

When `get_task_context` returns `canProceed=false`, or any `attentionItems` entry has `action=ask_user`, stop before planning or implementing. Surface the attention item and ask the user the provided question instead of choosing a side silently.

Use `report_conflict` when memories appear inconsistent. Surface the conflict to the user instead of deciding truth automatically.

Use `resolve_conflict` when the user clarifies which conflicting memory should remain authoritative. This marks superseded memories so normal retrieval stops returning them while preserving audit history through direct `get`.

Do not store ordinary conversation, temporary reasoning, or details only relevant to the current response.

Report the MBank agent instruction version from onboarding status. If MBank reports that this instruction block is missing, outdated, or not checkable from the server, ask the user before updating this file or running the bootstrap script. Do not modify agent instruction files without explicit user approval.
<!-- mbank:end -->
```
