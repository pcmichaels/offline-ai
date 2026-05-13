#!/usr/bin/env pwsh
# run-demo-programmatic-llm-interaction.ps1 - Runs the Programmatic LLM Interaction demo
# Idempotent: safe to re-run. Restores caller's working directory on exit.
#
# Usage:
#   .\run-demo-programmatic-llm-interaction.ps1           # Uses Prompt:Message from appsettings
#   .\run-demo-programmatic-llm-interaction.ps1 --prompt    # Same as above (explicit)
#   .\run-demo-programmatic-llm-interaction.ps1 --return-value-only   # Outputs 1 for Motorhead, 0 otherwise

$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Split-Path -Parent $scriptDir
$runDir = Join-Path (Join-Path $repoRoot "src") "programmatic-llm-interaction"
$csproj = Join-Path $runDir "ProgrammaticLlmInteraction.csproj"

if (-not (Test-Path $csproj)) {
    Write-Error "Project not found: $csproj"
}

Write-Host "Running Programmatic LLM Interaction demo..." -ForegroundColor Cyan
Write-Host "Ensure LM Studio is running with the local server started.`n"

Push-Location $runDir
try {
    dotnet run -- @args
}
finally {
    Pop-Location
}
