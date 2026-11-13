param(
    [ValidateSet("codex", "claude", "cursor", "all")]
    [string] $Client = "all",

    [string] $ServerUrl = $env:MBANK_SERVER_URL,

    [switch] $Yes
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($ServerUrl)) {
    throw "Pass -ServerUrl with the running MBank server URL or set MBANK_SERVER_URL."
}

function Get-Targets {
    switch ($Client) {
        "codex" { @([pscustomobject]@{ Target = "codex"; Path = "AGENTS.md" }) }
        "claude" { @([pscustomobject]@{ Target = "claude"; Path = "CLAUDE.md" }) }
        "cursor" { @([pscustomobject]@{ Target = "cursor"; Path = ".cursor/rules/mbank.mdc" }) }
        "all" {
            @(
                [pscustomobject]@{ Target = "codex"; Path = "AGENTS.md" },
                [pscustomobject]@{ Target = "claude"; Path = "CLAUDE.md" },
                [pscustomobject]@{ Target = "cursor"; Path = ".cursor/rules/mbank.mdc" }
            )
        }
    }
}

function Get-MBankInstructionBlock {
    $baseUrl = $ServerUrl.TrimEnd("/")
    $uri = "$baseUrl/onboarding/agent-instructions"

    try {
        $block = Invoke-RestMethod -Uri $uri -Method Get
    }
    catch {
        throw "Failed to fetch MBank instructions from $uri. Start MBank or pass -ServerUrl with the running MBank server URL."
    }

    if (-not ($block -is [string]) -or -not ($block.Contains("<!-- mbank:start"))) {
        throw "MBank server returned an invalid instruction block."
    }

    return $block
}

function Set-MBankBlock {
    param(
        [string] $Path,
        [string] $Block
    )

    $directory = Split-Path -Parent $Path
    if ($directory) {
        New-Item -ItemType Directory -Path $directory -Force | Out-Null
    }

    if (-not (Test-Path -LiteralPath $Path)) {
        Set-Content -LiteralPath $Path -Value $Block -NoNewline
        return
    }

    $content = Get-Content -LiteralPath $Path -Raw
    $pattern = '(?s)<!--\s*mbank:start\b.*?<!--\s*mbank:end\s*-->'
    if ($content -match $pattern) {
        $replaced = $false
        $updated = [regex]::Replace($content, $pattern, [System.Text.RegularExpressions.MatchEvaluator] {
            if ($replaced) {
                return ""
            }

            $replaced = $true
            return $Block
        })
        Set-Content -LiteralPath $Path -Value $updated -NoNewline
        return
    }

    $separator = if ($content.EndsWith("`n")) { "" } else { "`n" }
    Set-Content -LiteralPath $Path -Value "$content$separator`n$Block" -NoNewline
}

$targets = Get-Targets

Write-Host "MBank agent instruction bootstrap"
Write-Host ""
Write-Host "Fetching canonical instruction block from: $($ServerUrl.TrimEnd('/'))"
Write-Host ""

$block = Get-MBankInstructionBlock

Write-Host "This will add or update the versioned MBank block in:"
foreach ($target in $targets) {
    Write-Host "- $($target.Path)"
}
Write-Host ""
Write-Host "Repo: $(Get-Location)"
Write-Host ""

if (-not $Yes) {
    $answer = Read-Host "Proceed? [y/N]"
    if ($answer -notin @("y", "Y", "yes", "YES")) {
        Write-Host "No files changed."
        exit 0
    }
}

foreach ($target in $targets) {
    Set-MBankBlock -Path $target.Path -Block $block
    Write-Host "Updated $($target.Path)"
}

Write-Host ""
Write-Host "Done. The MBank block was fetched from $($ServerUrl.TrimEnd('/'))."
