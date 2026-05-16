# MBank

MCP-first memory storage and retrieval for AI agents.

## Current MVP

- ASP.NET Core 10 MCP server.
- Streamable HTTP MCP endpoint at `/mcp`.
- EF Core persistence with Postgres and pgvector.
- OpenAI `text-embedding-3-small` embeddings.
- No UI yet; simple bearer-protected admin provisioning endpoints exist for users, projects, and project tokens.

## Prerequisites

- .NET SDK 10.0.201 or compatible.
- Docker Desktop running with the Linux engine enabled.
- `OpenAI:ApiKey` configured through user secrets, environment-specific configuration, or another ASP.NET Core configuration provider.
- `MBANK_ADMIN_TOKEN` configured as an environment variable.

## Run Locally With Aspire

Aspire is the preferred local runtime when running multiple MBank worktrees in parallel. Each worktree runs its own AppHost, pgvector/Postgres container, database volume, migration worker, API process, and randomly assigned endpoints.

Configure the AppHost secrets once per worktree:

```powershell
dotnet user-secrets --project src\MBank.AppHost\MBank.AppHost.csproj set "Parameters:mbankAdminToken" "<admin-token>"
dotnet user-secrets --project src\MBank.AppHost\MBank.AppHost.csproj set "Parameters:openaiApiKey" "<openai-api-key>"
```

Start the local runtime:

```powershell
dotnet run --project src\MBank.AppHost\MBank.AppHost.csproj
```

The Aspire dashboard shows the active API endpoint. The `migrate` resource applies EF Core migrations before the `api` resource starts.

Production deployment does not use the local AppHost. Use `scripts\Deploy-MBankHetzner.ps1` for the current Hetzner deploy path so unrelated production services are not touched.

## Run Locally With Docker Compose

Start Postgres with pgvector:

```powershell
docker compose up -d
```

Apply the EF schema:

```powershell
dotnet ef database update --project src\MBank.Api\MBank.Api.csproj --startup-project src\MBank.Api\MBank.Api.csproj
```

Run the MCP server:

```powershell
$env:MBANK_ADMIN_TOKEN = "<admin-token>"
dotnet run --project src\MBank.Api\MBank.Api.csproj
```

The MCP endpoint is available at:

```text
http://localhost:5221/mcp
```

The default launch profile uses `http://localhost:5221`. The root endpoint returns the active status payload.

## Provisioning

Admin provisioning endpoints require:

```text
Authorization: Bearer <admin-token>
```

Available endpoints:

- `POST /admin/users`
- `POST /admin/projects`
- `POST /admin/project-tokens`

Project tokens are returned once when created. Store only the returned token in client configuration; MBank stores its hash.

Example Codex repo-scoped MCP configuration:

```toml
[mcp_servers.mbank]
enabled = true
url = "http://localhost:5221/mcp"
bearer_token_env_var = "MBANK_PROJECT_TOKEN"
```

Set `MBANK_PROJECT_TOKEN` to the project token created through the admin endpoint. `/mcp` rejects requests without a valid project token.

## Local Memory Porting

To preserve memories from the old unscoped local durable database, export before applying the project-token schema migration:

```powershell
.\scripts\Port-LocalMemories.ps1 -Mode Export
```

After provisioning the target user/project in the new database, import the export into that scope:

```powershell
.\scripts\Port-LocalMemories.ps1 -Mode Import -File .\artifacts\mbank-local-memory-export-YYYYMMDD-HHMMSS.json -UserId <user-id> -ProjectId <project-id>
```

The porting script preserves memory, source, and link IDs, and adds the target `UserId`/`ProjectId` during import.

## MCP Tools

- `StartTask`: returns current MBank agent rules, working-style guidance, and instruction-file onboarding status for the current task.
- `Remember`: stores durable product or implementation guidance as one or more atomic memories.
- `GetTaskContext`: retrieves strong and possible memory matches for a structured task before planning or implementation.
- `Get`: fetches one memory with source evidence and linked memories.
- `ReportConflict`: records that memories appear to conflict and should be clarified later.
- `CheckOnboarding`: reports whether supported repo agent instruction files contain the current versioned MBank block.

## Agent Instruction Bootstrap

Adding the MCP server makes MBank available to an agent. For reliable behavior, each repo should also include a small MBank instruction block in the agent instruction file.

The bootstrap scripts fetch the canonical instruction block from a running MBank server. They do not carry a fallback copy of the instructions. If MBank is not running, bootstrap fails instead of writing stale guidance. The server does not know about Codex, Claude, Cursor, or file placement; the scripts handle that mapping locally.

The bootstrap scripts only add or update content between `<!-- mbank:start ... -->` and `<!-- mbank:end -->`. They prompt before changing files by default.

When the MBank instruction text changes, bump the canonical block version in the server so agents can detect stale repo instructions.

Placeholder public URL:

```bash
curl -fsSL https://mbank.dev/bootstrap-agent.sh | sh
```

Inspect-first usage:

```bash
curl -fsSL https://mbank.dev/bootstrap-agent.sh -o bootstrap-agent.sh
less bootstrap-agent.sh
sh bootstrap-agent.sh
```

Local development usage:

```powershell
.\scripts\bootstrap-agent.ps1 -Client codex
```

```bash
sh scripts/bootstrap-agent.sh --client codex
```

If MBank is running somewhere other than `http://localhost:5221`, pass the server URL:

```powershell
.\scripts\bootstrap-agent.ps1 -Client codex -ServerUrl http://localhost:5222
```

```bash
sh scripts/bootstrap-agent.sh --client codex --server-url http://localhost:5222
```

Supported clients:

- `codex`: updates `AGENTS.md`.
- `claude`: updates `CLAUDE.md`.
- `cursor`: updates `.cursor/rules/mbank.mdc`.
- `all`: updates all supported targets.

Noninteractive usage is available with `--yes` or `-Yes`, but repo instruction updates should still be explicitly approved by the user before an agent runs them.

Manual block:

```md
<!-- mbank:start version="1.0.3" -->
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

Do not store ordinary conversation, temporary reasoning, or details only relevant to the current response.

If MBank reports that this instruction block is missing or outdated, ask the user before updating this file or running the bootstrap script. Do not modify agent instruction files without explicit user approval.
<!-- mbank:end -->
```
