$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Resolve-Path (Join-Path $scriptDir "..")
$targetRoot = Join-Path $repoRoot "MBank-local"
$publishDir = Join-Path $targetRoot "app"
$apiProject = Join-Path $repoRoot "src\MBank.Api\MBank.Api.csproj"
$testProject = Join-Path $repoRoot "tests\MBank.Tests\MBank.Tests.csproj"

function New-Secret {
    $bytes = [byte[]]::new(32)
    $rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
    try {
        $rng.GetBytes($bytes)
    }
    finally {
        $rng.Dispose()
    }

    return [Convert]::ToBase64String($bytes)
}

function Assert-Success($CommandName) {
    if ($LASTEXITCODE -ne 0) {
        throw "$CommandName failed with exit code $LASTEXITCODE."
    }
}

function Write-TextFile($Path, $Content) {
    $directory = Split-Path -Parent $Path
    if (-not (Test-Path $directory)) {
        New-Item -ItemType Directory -Path $directory | Out-Null
    }

    Set-Content -LiteralPath $Path -Value $Content -Encoding UTF8
}

dotnet test $testProject -c Release -p:UseAppHost=false
Assert-Success "dotnet test"

New-Item -ItemType Directory -Force -Path $targetRoot | Out-Null

$envPath = Join-Path $targetRoot ".env"
if (-not (Test-Path $envPath)) {
    $envContent = @"
POSTGRES_PASSWORD=$(New-Secret)
MBANK_ADMIN_TOKEN=$(New-Secret)
OPENAI_API_KEY=
"@
    Write-TextFile $envPath $envContent
    Write-Host "Created MBank-local\.env. Set OPENAI_API_KEY before starting the API."
}
else {
    $envContent = Get-Content -LiteralPath $envPath
    $envContent = $envContent | Where-Object {
        ($_ -notlike "MCP_ACCESS_HEADER_NAME=*") -and ($_ -notlike "MCP_ACCESS_HEADER_VALUE=*")
    }
    if (-not ($envContent | Where-Object { $_ -like "MBANK_ADMIN_TOKEN=*" })) {
        $envContent += "MBANK_ADMIN_TOKEN=$(New-Secret)"
    }
    Set-Content -LiteralPath $envPath -Value $envContent -Encoding UTF8
}

$composeContent = @"
name: mbank-local

services:
  postgres:
    image: pgvector/pgvector:pg16
    environment:
      POSTGRES_DB: mbank_local
      POSTGRES_USER: mbank
      POSTGRES_PASSWORD: `${POSTGRES_PASSWORD}
    ports:
      - "5433:5432"
    volumes:
      - postgres-data:/var/lib/postgresql/data
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U mbank -d mbank_local"]
      interval: 5s
      timeout: 5s
      retries: 10

  api:
    image: mcr.microsoft.com/dotnet/aspnet:10.0
    depends_on:
      postgres:
        condition: service_healthy
    working_dir: /app
    command: ["dotnet", "MBank.Api.dll"]
    environment:
      ASPNETCORE_ENVIRONMENT: Production
      ASPNETCORE_URLS: http://+:8080
      ConnectionStrings__MemoryDb: Host=postgres;Port=5432;Database=mbank_local;Username=mbank;Password=`${POSTGRES_PASSWORD}
      MBANK_ADMIN_TOKEN: `${MBANK_ADMIN_TOKEN}
      OpenAI__ApiKey: `${OPENAI_API_KEY}
      OpenAI__EmbeddingModel: text-embedding-3-small
      OpenAI__EmbeddingDimensions: "1536"
    ports:
      - "5222:8080"
    volumes:
      - ./app:/app:ro
    restart: unless-stopped

volumes:
  postgres-data:
"@
Write-TextFile (Join-Path $targetRoot "docker-compose.yml") $composeContent

$startContent = @'
$ErrorActionPreference = "Stop"
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
docker compose --env-file (Join-Path $here ".env") -f (Join-Path $here "docker-compose.yml") up -d
'@
Write-TextFile (Join-Path $targetRoot "Start-MBankLocal.ps1") $startContent

$stopContent = @'
$ErrorActionPreference = "Stop"
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
docker compose --env-file (Join-Path $here ".env") -f (Join-Path $here "docker-compose.yml") down
'@
Write-TextFile (Join-Path $targetRoot "Stop-MBankLocal.ps1") $stopContent

$migrateContent = @'
$ErrorActionPreference = "Stop"
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Resolve-Path (Join-Path $here "..")

docker compose --env-file (Join-Path $here ".env") -f (Join-Path $here "docker-compose.yml") up -d postgres

$postgresPasswordLine = Get-Content (Join-Path $here ".env") | Where-Object { $_ -like "POSTGRES_PASSWORD=*" } | Select-Object -First 1
if (-not $postgresPasswordLine) {
    throw "POSTGRES_PASSWORD is missing from .env."
}

$postgresPassword = $postgresPasswordLine -replace "^POSTGRES_PASSWORD=", ""
$env:ConnectionStrings__MemoryDb = "Host=localhost;Port=5433;Database=mbank_local;Username=mbank;Password=$postgresPassword"
$env:OpenAI__ApiKey = "migration-placeholder"
$env:OpenAI__EmbeddingModel = "text-embedding-3-small"
$env:OpenAI__EmbeddingDimensions = "1536"

dotnet ef database update --configuration Release --project (Join-Path $repoRoot "src\MBank.Api\MBank.Api.csproj") --startup-project (Join-Path $repoRoot "src\MBank.Api\MBank.Api.csproj")
if ($LASTEXITCODE -ne 0) {
    throw "dotnet ef database update failed with exit code $LASTEXITCODE."
}
'@
Write-TextFile (Join-Path $targetRoot "Update-Database.ps1") $migrateContent

$readmeContent = @'
# MBank Local Durable Instance

This directory is generated and intentionally ignored by git.

## First run

1. Edit `.env` and set `OPENAI_API_KEY`.
2. Run:

```powershell
.\Update-Database.ps1
.\Start-MBankLocal.ps1
```

The MCP endpoint is:

```text
http://localhost:5222/mcp
```

Admin provisioning endpoints require the `MBANK_ADMIN_TOKEN` value from `.env` as:

```text
Authorization: Bearer <MBANK_ADMIN_TOKEN>
```

## Manual publish/update

From the repo root:

```powershell
.\scripts\Publish-MBankLocal.ps1
.\MBank-local\Update-Database.ps1
.\MBank-local\Start-MBankLocal.ps1
```
'@
Write-TextFile (Join-Path $targetRoot "README.md") $readmeContent

dotnet publish $apiProject -c Release -o $publishDir --no-self-contained
Assert-Success "dotnet publish"

Write-Host "Published MBank.Api to MBank-local\app."
Write-Host "Run MBank-local\Update-Database.ps1, then MBank-local\Start-MBankLocal.ps1."
