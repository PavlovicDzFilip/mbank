# MBank

MCP-first memory storage and retrieval for AI agents.

## Current MVP

- ASP.NET Core 10 MCP server.
- Streamable HTTP MCP endpoint at `/mcp`.
- EF Core persistence with Postgres and pgvector.
- OpenAI `text-embedding-3-small` embeddings.
- No UI yet.

## Prerequisites

- .NET SDK 10.0.201 or compatible.
- Docker Desktop running with the Linux engine enabled.
- `OpenAI:ApiKey` configured through user secrets, environment-specific configuration, or another ASP.NET Core configuration provider.

## Run Locally

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
dotnet run --project src\MBank.Api\MBank.Api.csproj
```

The MCP endpoint is available at:

```text
http://localhost:5221/mcp
```

The default launch profile uses `http://localhost:5221`. The root endpoint returns the active status payload.

## MCP Tools

- `Remember`: stores durable product or implementation guidance as one or more atomic memories.
- `GetTaskContext`: retrieves relevant memories for a structured task before planning or implementation.
- `Get`: fetches one memory with source evidence and linked memories.
- `ReportConflict`: records that memories appear to conflict and should be clarified later.
