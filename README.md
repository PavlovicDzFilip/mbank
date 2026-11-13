# MBank

MCP-first memory storage and retrieval for AI agents.

## Features

- ASP.NET Core 10 MCP server.
- Streamable HTTP MCP endpoint at `/mcp`.
- EF Core persistence with Postgres and pgvector.
- OpenAI `text-embedding-3-small` embeddings.
- Magic-link login at `/login`.
- Signed-in project workspace at `/projects` for users, projects, and project tokens.

## Prerequisites

- .NET SDK **10.0.103** exactly (`global.json` pins this version for local dev, CI, and Docker). Install with `dotnet-install.sh --version 10.0.103`, or use a Fedora/RHEL package that ships SDK 10.0.103.
- A running Docker engine with Linux container support.
- An OpenAI API key for embeddings and a password for the local Postgres instance.

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

The AppHost starts the Aspire dashboard, applies database migrations through the `migrate` resource, then starts the `api` resource. Development magic-link email goes to the `mailpit` resource.

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

### Running without Aspire

Supply a Postgres database with pgvector, an OpenAI API key, and SMTP settings. [docs/mbank.env.example](docs/mbank.env.example) lists the environment variables. Set `MagicLink__PublicBaseUrl` to the instance's external base URL when serving MBank behind a proxy. MBank uses that URL for sign-in links, MCP client configuration, and agent setup links.

Set `ConnectionStrings__MemoryDb` for both processes and apply migrations before starting the API. If you are upgrading a database with legacy users whose email is null, populate their email addresses before running the migrations.

```sh
dotnet run --project src/MBank.MigrationService
dotnet run --project src/MBank.Api
```

## Provisioning

Sign in at `/login`, then create projects and project tokens in `/projects`. Project tokens are returned once when created. Store only the returned token in client configuration; MBank stores its hash plus non-secret display prefix/suffix metadata.

Set `MBANK_PROJECT_TOKEN` to a project token created in the UI. `/mcp` rejects requests without a valid project token. Point your MCP client at `<api-base-url>/mcp` (see Aspire dashboard when running locally).

## MCP Tools

- `StartTask`: returns current MBank agent rules, working-style guidance, and instruction status for the current task.
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

Run these commands from the root of the repository you want to configure. Replace `https://mbank.example.com` with the base URL of your MBank instance. For local development, copy the `api` URL from the Aspire dashboard.

```powershell
$mbankUrl = "https://mbank.example.com"
Invoke-WebRequest "$mbankUrl/bootstrap-agent.ps1" -OutFile bootstrap-agent.ps1
.\bootstrap-agent.ps1 -Client codex -ServerUrl $mbankUrl
```

```bash
mbank_url="https://mbank.example.com"
curl -fsSL "$mbank_url/bootstrap-agent.sh" -o bootstrap-agent.sh &&
  sh bootstrap-agent.sh --client codex --server-url "$mbank_url"
```

Supported clients:

- `codex`: updates `AGENTS.md`.
- `claude`: updates `CLAUDE.md`.
- `cursor`: updates `.cursor/rules/mbank.mdc`.
- `all`: updates all supported targets.

Noninteractive usage is available with `--yes` or `-Yes`, but repo instruction updates should still be explicitly approved by the user before an agent runs them.

You can also set `MBANK_SERVER_URL` in place of the URL argument.

## Build and test

```sh
dotnet build MBank.slnx -c Release
dotnet test tests/MBank.Tests/MBank.Tests.csproj -c Release
```

Integration tests start a pgvector container through Testcontainers and require a running Docker engine. They use test doubles for email and embeddings.

## License

MBank is available under the [MIT License](LICENSE).
