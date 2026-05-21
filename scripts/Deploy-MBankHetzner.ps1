# Emergency fallback only. Primary production deploy: .github/workflows/deploy.yml (GitHub Actions).
$ErrorActionPreference = "Stop"

$registry = "registry.engineeringwithfilip.com"
$sshTarget = "root@188.245.186.124"
$remoteDir = "/vault"
$tag = Get-Date -Format "yyyyMMddHHmmss"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$dockerfile = Join-Path $repoRoot "src\MBank.Api\Dockerfile"
$testProject = Join-Path $repoRoot "tests\MBank.Tests\MBank.Tests.csproj"
$apiImage = "$registry/mbank/api"
$migrateImage = "$registry/mbank/migrate"

function Assert-Success($CommandName) {
    if ($LASTEXITCODE -ne 0) {
        throw "$CommandName failed with exit code $LASTEXITCODE."
    }
}

Write-Host "Deploying MBank $tag"

dotnet test $testProject -c Release -p:UseAppHost=false
Assert-Success "dotnet test"

docker build -f $dockerfile --target api -t "${apiImage}:$tag" $repoRoot
Assert-Success "docker build api"

docker build -f $dockerfile --target migrate -t "${migrateImage}:$tag" $repoRoot
Assert-Success "docker build migrate"

docker login $registry
Assert-Success "docker login"

docker push "${apiImage}:$tag"
Assert-Success "docker push api:$tag"

docker push "${migrateImage}:$tag"
Assert-Success "docker push migrate:$tag"

$remoteScript = @"
set -euo pipefail
cd "$remoteDir"
test -f mbank.env
export MBANK_VERSION="$tag"
docker compose pull mbank-api mbank-migrate
docker compose --profile mbank-migrate run --rm mbank-migrate
docker compose up -d --no-deps --force-recreate mbank-api
"@

$remoteScript = $remoteScript -replace "`r`n", "`n"
$remoteScript | ssh $sshTarget "bash -s"
Assert-Success "remote deploy"

Write-Host "MBank deploy complete: $tag"
