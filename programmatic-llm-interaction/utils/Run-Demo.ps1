#!/usr/bin/env pwsh
# Run-Demo.ps1 - Runs the Programmatic LLM Interaction demo
# Idempotent: safe to re-run. Restores caller's working directory on exit.

$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$demoRoot = Split-Path -Parent $scriptDir
$srcPath = Join-Path $demoRoot "src"

if (-not (Test-Path $srcPath)) {
    Write-Error "Source folder not found: $srcPath"
}

$csproj = Join-Path $srcPath "ProgrammaticLlmInteraction.csproj"
if (-not (Test-Path $csproj)) {
    Write-Error "Project file not found: $csproj"
}

Write-Host "Running Programmatic LLM Interaction demo..." -ForegroundColor Cyan
Write-Host "Ensure LM Studio is running with the local server started.`n"

Push-Location $srcPath
try {
    dotnet run
}
finally {
    Pop-Location
}
