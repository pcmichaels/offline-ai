#!/usr/bin/env pwsh
# run-demo-programmatic-llm-interaction-structured.ps1 - Structured band JSON demo
# Idempotent: safe to re-run. Restores caller's working directory on exit.

$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Split-Path -Parent $scriptDir
$runDir = Join-Path (Join-Path $repoRoot "src") "programmatic-llm-interaction-structured"
$csproj = Join-Path $runDir "ProgrammaticLlmInteractionStructured.csproj"

if (-not (Test-Path $csproj)) {
    Write-Error "Project not found: $csproj"
}

Write-Host "Running Programmatic LLM Interaction (structured JSON demo)..." -ForegroundColor Cyan
Write-Host "Ensure LM Studio is running with the local server started.`n"

Push-Location $runDir
try {
    dotnet run
}
finally {
    Pop-Location
}
