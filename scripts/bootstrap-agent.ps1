param(
    [ValidateSet("codex", "claude", "cursor", "all")]
    [string] $Client = "codex",

    [switch] $Yes
)

$ErrorActionPreference = "Stop"

# Bump this version whenever the MBank instruction block changes.
$Version = "1.0.0"
$Block = @"
<!-- mbank:start version="$Version" -->
## MBank Memory

Use the configured ``mbank`` MCP server proactively for durable project memory.

At the start of a new product, planning, implementation, debugging, or review task, call ``get_task_context`` with a structured task description before proposing a plan or making changes.

Use ``remember`` when the user provides durable product or implementation guidance that is likely to matter in future sessions, such as product direction, architectural decisions, coding conventions, constraints, or next steps.

Use ``get`` to inspect full memory details when a retrieved memory needs source context.

Use ``report_conflict`` when memories appear inconsistent. Surface the conflict to the user instead of deciding truth automatically.

Do not store ordinary conversation, temporary reasoning, or details only relevant to the current response.

If MBank reports that this instruction block is missing or outdated, ask the user before updating this file or running the bootstrap script. Do not modify agent instruction files without explicit user approval.
<!-- mbank:end -->
"@

function Get-Targets {
    switch ($Client) {
        "codex" { @("AGENTS.md") }
        "claude" { @("CLAUDE.md") }
        "cursor" { @(".cursor/rules/mbank.mdc") }
        "all" { @("AGENTS.md", "CLAUDE.md", ".cursor/rules/mbank.mdc") }
    }
}

function Set-MBankBlock {
    param([string] $Path)

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
        $updated = [regex]::Replace($content, $pattern, [System.Text.RegularExpressions.MatchEvaluator] { $Block }, 1)
        Set-Content -LiteralPath $Path -Value $updated -NoNewline
        return
    }

    $separator = if ($content.EndsWith("`n")) { "" } else { "`n" }
    Set-Content -LiteralPath $Path -Value "$content$separator`n$Block" -NoNewline
}

$targets = Get-Targets

Write-Host "MBank agent instruction bootstrap"
Write-Host ""
Write-Host "This will add or update the versioned MBank block in:"
foreach ($target in $targets) {
    Write-Host "- $target"
}
Write-Host ""
Write-Host "Version: $Version"
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
    Set-MBankBlock -Path $target
    Write-Host "Updated $target"
}

Write-Host ""
Write-Host "Done. The MBank block is version $Version."
